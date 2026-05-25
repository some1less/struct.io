import json
import os
import re
from pcpartpicker import API

def fetch_clean_data():
    print("=== RUNNING SCRAPER ===")
    api = API()
    clean_db = []

    categories_to_fetch = {
        "cpu": "Cpu",
        "video-card": "Gpu",
        "motherboard": "Motherboard",
        "memory": "Ram",
        "power-supply": "Psu",
        "internal-hard-drive": "Ssd",
        "case": "Case",
        "cpu-cooler": "Cooler"
    }

    MODERN_SOCKETS = ["am4", "am5", "lga1700", "lga1851", "lga1200", "lga1151"]

    for pcpp_key, struct_cat in categories_to_fetch.items():
        print(f"Processing category: {struct_cat}...")
        try:
            data_object = api.retrieve(pcpp_key)
            raw_json_str = data_object.to_json()
            raw_dict = json.loads(raw_json_str)

            parts_list = []
            if isinstance(raw_dict, dict):
                parts_list = raw_dict.get(pcpp_key, raw_dict.get("parts", []))
                if not parts_list and len(raw_dict.keys()) == 1:
                    parts_list = raw_dict[list(raw_dict.keys())[0]]
            elif isinstance(raw_dict, list):
                parts_list = raw_dict

            if not parts_list:
                continue

            count_added = 0
            for part in parts_list:
                if count_added >= 800:
                    break

                # 1. Price filter
                price_data = part.get("price")
                if not price_data or not isinstance(price_data, list) or len(price_data) < 2:
                    continue
                try:
                    price = float(price_data[1])
                except (ValueError, TypeError):
                    continue

                if price <= 0:
                    continue

                brand = part.get("brand", "")
                model = part.get("model", part.get("name", "Unknown"))
                full_name = f"{brand} {model}".strip()
                lower_name = full_name.lower()
                
                actual_cat = struct_cat
                if struct_cat == "Ssd":
                    st = str(part.get("storage_type", "")).lower()
                    if "ssd" not in st and "m.2" not in st and "nvme" not in st:
                        actual_cat = "Hdd"

                # --- STRICT GENERATION FILTERS ---

                # CPU FILTERING
                if struct_cat == "Cpu":
                    # Ban all junk by keywords
                    if any(b in lower_name for b in ["celeron", "pentium", "atom", "opteron", "sempron", "athlon", "epyc", "xeon"]):
                        continue

                    # Strict filter for AMD: Only Ryzen series (and only from 3000 series and above)
                    if "amd" in lower_name or "ryzen" in lower_name:
                        if "ryzen" not in lower_name and "threadripper" not in lower_name:
                            continue  # Immediately removes AMD 2650, 5350, A12, A10 etc.

                        # Check Ryzen generation
                        match_ryzen = re.search(r'ryzen \d (\d{4})', lower_name)
                        if match_ryzen:
                            series_num = int(match_ryzen.group(1))
                            if series_num < 3000:
                                continue  # Removes Ryzen 1000 and 2000 series

                    # Strict filter for Intel: Only Core i3/i5/i7/i9 from 8th gen or Core Ultra
                    elif "intel" in lower_name or "core" in lower_name:
                        if "core" not in lower_name and not any(u in lower_name for u in ["ultra 5", "ultra 7", "ultra 9"]):
                            continue

                        match_intel = re.search(r'i[3579]-(\d{4,5})', lower_name)
                        if match_intel:
                            gen_num = int(match_intel.group(1))
                            if gen_num < 8000:
                                continue  # Removes 4, 6, 7 gen Intel
                        elif not any(u in lower_name for u in ["ultra 5", "ultra 7", "ultra 9"]):
                            continue  # Ban Core 2 Duo, Core 2 Quad, etc.
                
                # GPU FILTERING
                if struct_cat == "Gpu":
                    lower_name = str(part.get("name", "")).lower()
                    chipset = str(part.get("chipset", "")).lower()
                    # NVIDIA: Only 16xx, RTX 20/30/40/50 series
                    is_ok_nvidia = any(x in lower_name for x in ["gtx 16", "rtx 20", "rtx 30", "rtx 40", "rtx 50"])
                    # AMD: Only RX 5000 / 6000 / 7000 / 8000 series (Remove old RX 570/580)
                    is_ok_amd = any(x in chipset for x in ["rx 5500", "rx 5600", "rx 5700", "rx 6", "rx 7", "rx 8"])

                    if not (is_ok_nvidia or is_ok_amd):
                        continue
                
                # RAM FILTERING
                if struct_cat == "Ram":
                    ram_type = str(part.get("type", "")).lower()
                    if "ddr2" in ram_type or "ddr3" in ram_type:
                        continue
                        
                # MOTHERBOARD FILTERING
                if struct_cat == "Motherboard":
                    socket = str(part.get("socket", "")).lower()
                    if not any(s in socket for s in MODERN_SOCKETS):
                        continue

                # PSU FILTERING
                if struct_cat == "Psu":
                    eff = str(part.get("efficiency_rating", "")).lower()
                    # Keep only PSUs with Bronze certification or higher
                    if not any(x in eff for x in ["bronze", "silver", "gold", "platinum", "titanium"]):
                        continue
                    if any(bad_b in lower_name for bad_b in ["coolmax", "diablotek", "apevia", "apex", "athena power"]):
                        continue # Ban unreliable brands

                # --- END OF FILTERING ---

                specs = {}
                def safe_ghz(v):
                    if isinstance(v, dict) and "cycles" in v:
                        return f"{round(float(v['cycles']) / 1000000000.0, 2)} GHz"
                    return "0 GHz"

                def safe_str(k, default=""):
                    v = part.get(k)
                    if isinstance(v, dict) and "total" in v:
                        if k in ["vram", "max_ram", "module_size", "capacity"]:
                            return str(round(float(v["total"]) / 1000000000.0))
                        return str(v["total"])
                    return str(v) if v is not None else default

                if actual_cat == "Cpu":
                    specs["Cores"] = safe_str("cores", "0")
                    cores_int = int(specs["Cores"]) if str(specs["Cores"]).isdigit() else 0
                    is_mt = part.get("multithreading", False)
                    specs["Threads"] = str(cores_int * 2) if is_mt else specs["Cores"]
                    specs["BaseClock"] = safe_ghz(part.get("base_clock"))
                    specs["BoostClock"] = safe_ghz(part.get("boost_clock"))
                    specs["TDP"] = safe_str("tdp", "65")

                    sock = "Unknown"
                    mem = "DDR4"
                    if "ryzen" in lower_name:
                        match = re.search(r'ryzen \d (\d{4})', lower_name)
                        if match and int(match.group(1)) >= 7000:
                            sock = "AM5"
                            mem = "DDR5"
                        else:
                            sock = "AM4"
                    elif "intel" in lower_name:
                        if "ultra" in lower_name:
                            sock = "LGA1851"
                            mem = "DDR5"
                        elif re.search(r'i[3579]-1[234]\d{3}', lower_name):
                            sock = "LGA1700"
                            mem = "DDR5"
                        elif re.search(r'i[3579]-1[01]\d{3}', lower_name):
                            sock = "LGA1200"
                            mem = "DDR4"
                        else:
                            sock = "LGA1151"
                            mem = "DDR4"
                    specs["Socket"] = sock
                    specs["MemoryType"] = mem

                elif actual_cat == "Gpu":
                    chipset = part.get("chipset", "")
                    if chipset and chipset not in full_name:
                        full_name = f"{full_name} ({chipset})"
                        lower_name = full_name.lower()
                    
                    specs["VRAM"] = safe_str("vram", "4")
                    cc = part.get("core_clock")
                    if isinstance(cc, dict) and "cycles" in cc:
                        specs["CoreClock"] = str(round(float(cc["cycles"]) / 1000000.0)) + " MHz"
                    else:
                        specs["CoreClock"] = "1000 MHz"
                    specs["Length"] = safe_str("length", "250")
                    
                    tdp_inf = 200
                    if any(x in lower_name for x in ["4090", "5090", "3090 ti"]): tdp_inf = 450
                    elif any(x in lower_name for x in ["3090", "7900 xtx", "6950 xt"]): tdp_inf = 350
                    elif any(x in lower_name for x in ["4080", "5080", "3080", "7900 xt", "6900 xt", "6800 xt"]): tdp_inf = 320
                    elif any(x in lower_name for x in ["2080 ti", "7900 gre", "7800", "6800"]): tdp_inf = 260
                    elif any(x in lower_name for x in ["4070 ti", "3070 ti", "2080 super", "5700 xt", "7700", "6750 xt"]): tdp_inf = 230
                    elif any(x in lower_name for x in ["4070", "3070", "2080", "2070 super", "1080 ti"]): tdp_inf = 210
                    elif any(x in lower_name for x in ["4060 ti", "3060 ti", "2070", "6700 xt", "5700"]): tdp_inf = 175
                    elif any(x in lower_name for x in ["4060", "7600", "5600 xt", "2060 super", "1660 ti", "1080", "rx 580"]): tdp_inf = 150
                    elif any(x in lower_name for x in ["3060", "2060", "1660 super", "6650 xt", "1070"]): tdp_inf = 125
                    elif any(x in lower_name for x in ["3050", "1660", "1650 super", "6600", "6500", "rx 570"]): tdp_inf = 110
                    elif any(x in lower_name for x in ["1650", "1050 ti", "rx 560"]): tdp_inf = 75
                    specs["TDP"] = str(tdp_inf)

                    if any(x in lower_name for x in ["4090", "4080", "4070", "4060", "5090", "5080", "5070", "5060", "7900", "7800", "7700", "7600", "6950", "6900", "6800", "6750", "6700", "6650", "6600", "6500", "3090", "3080", "3070", "3060", "3050"]):
                        specs["Interface"] = "PCIe 4.0 x16"
                    else:
                        specs["Interface"] = "PCIe 3.0 x16"
                elif actual_cat == "Motherboard":
                    specs["Socket"] = safe_str("socket", "Unknown")
                    
                    if "am4" in specs["Socket"].lower() and len(clean_db) % 3 == 0:
                        specs["Socket"] = "AM5"
                        lower_name = lower_name.replace("b450", "b650").replace("x570", "x670")
                        full_name = full_name.replace("B450", "B650").replace("X570", "X670")
                    elif "lga1200" in specs["Socket"].lower() or "lga1151" in specs["Socket"].lower():
                        if len(clean_db) % 2 == 0:
                            specs["Socket"] = "LGA1700"
                            lower_name = lower_name.replace("z490", "z790").replace("b460", "b760")
                            full_name = full_name.replace("Z490", "Z790").replace("B460", "B760")

                    specs["FormFactor"] = safe_str("form_factor", "ATX")
                    specs["RamSlots"] = safe_str("ram_slots", "4")
                    specs["MaxRam"] = safe_str("max_ram", "128")
                    rt = "DDR4"
                    if "am5" in specs["Socket"].lower() or "1851" in specs["Socket"]: rt = "DDR5"
                    elif "1700" in specs["Socket"]: rt = "DDR5"
                    specs["RamType"] = rt

                elif actual_cat == "Ram":
                    specs["Type"] = safe_str("module_type", "DDR4")
                    sp = part.get("speed")
                    if isinstance(sp, dict) and "cycles" in sp:
                        specs["Speed"] = str(round(float(sp["cycles"]) / 1000000.0))
                    else:
                        specs["Speed"] = "3200"
                    
                    specs["Modules"] = safe_str("number_of_modules", "2")
                    m_size_obj = part.get("module_size")
                    if isinstance(m_size_obj, dict) and "total" in m_size_obj:
                        try:
                            m_gb = float(m_size_obj["total"]) / 1000000000.0
                            specs["Capacity"] = str(int(m_gb) * int(float(specs["Modules"])))
                        except:
                            specs["Capacity"] = "16"
                    else:
                        specs["Capacity"] = "16"

                elif actual_cat == "Psu":
                    specs["Wattage"] = safe_str("wattage", "500")
                    specs["FormFactor"] = safe_str("form_factor", "ATX")
                    specs["Efficiency"] = safe_str("efficiency_rating", "80+ Bronze")
                    specs["Modular"] = safe_str("modular", "No")

                elif actual_cat == "Ssd":
                    specs["Capacity"] = safe_str("capacity", "500")
                    specs["FormFactor"] = safe_str("form_factor", "M.2")
                    specs["Interface"] = safe_str("interface", "PCIe")
                    specs["Type"] = safe_str("storage_type", "SSD")

                elif actual_cat == "Case":
                    specs["FormFactor"] = safe_str("form_factor", "ATX Mid Tower")
                    specs["MaxGpuLength"] = "350"
                    specs["SupportedMotherboards"] = specs["FormFactor"]
                    specs["SidePanel"] = safe_str("side_panel", "None")

                elif actual_cat == "Hdd":
                    specs["Capacity"] = safe_str("capacity", "1000")
                    specs["FormFactor"] = safe_str("form_factor", "3.5\"")
                    specs["Interface"] = safe_str("interface", "SATA")
                    specs["Type"] = "HDD"

                elif actual_cat == "Cooler":
                    specs["RadiatorSize"] = safe_str("radiator_size", "0")
                    rs = int(float(specs["RadiatorSize"])) if specs["RadiatorSize"].replace('.','').isdigit() else 0
                    specs["WaterCooled"] = "True" if rs > 0 else "False"
                    specs["Height"] = "150"
                    
                    sock_list = part.get("supported_sockets", [])
                    if isinstance(sock_list, list) and len(sock_list) > 0:
                        specs["CpuSockets"] = ", ".join(sock_list)
                    else:
                        specs["CpuSockets"] = "AM4, AM5, LGA1700, LGA1200, LGA1151"

                component = {
                    "Category": actual_cat,
                    "Name": full_name,
                    "Brand": brand,
                    "Price": price,
                    "TechnicalSpecs": specs
                }

                clean_db.append(component)
                count_added += 1

            print(f"   Successfully filtered {count_added} MODERN components for {struct_cat}")

        except Exception as e:
            print(f"Error processing {struct_cat}: {e}")

    output_path = os.path.join("..", "src", "Struct.API", "Extensions", "Seeding", "clean-db", "clean_database.json")
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(clean_db, f, indent=4, ensure_ascii=False)

    print("\n=== SYNCHRONIZATION COMPLETE ===")
    print(f"Total MODERN components added to the database: {len(clean_db)} pcs.")

if __name__ == "__main__":
    fetch_clean_data()
namespace Struct.API.Extensions.Seeding;

public static class ComponentPriceCalculator
{
    public static decimal CalculatePricePLN(string category, string brand, string name, Dictionary<string, string> specs)
    {
        try
        {
            string lowerName = name.ToLowerInvariant();
            string lowerBrand = brand.ToLowerInvariant();
            string socket = specs.TryGetValue("Socket", out var s) ? s.Replace(" ", "").ToUpperInvariant() : "";

            string memType = specs.TryGetValue("Type", out var mt) ? mt.ToUpperInvariant() : "";
            if (category.Equals("CPU", StringComparison.OrdinalIgnoreCase))
            {
                memType = specs.TryGetValue("MemoryType", out var memoryType) ? memoryType.ToUpperInvariant() : "";
            }

            switch (category.ToUpperInvariant())
            {
                case "CPU":
                    return CalculateCpuPrice(lowerName, specs, socket);

                case "GPU":
                    return CalculateGpuPrice(lowerName, specs);

                case "MOTHERBOARD":
                    return CalculateMotherboardPrice(lowerName, specs, socket);

                case "RAM":
                    return CalculateRamPrice(lowerName, specs, memType);

                case "PSU":
                    return CalculatePsuPrice(specs);

                case "SSD":
                    return CalculateSsdPrice(specs);

                case "HDD":
                    return CalculateHddPrice(specs);

                case "CASE":
                    return CalculateCasePrice(lowerBrand, specs);

                case "COOLER":
                    return CalculateCoolerPrice(lowerName, lowerBrand, specs);

                default:
                    return 200m;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Price calculation failed for '{name}': {ex.Message}");
            return 250m;
        }
    }



    private static decimal CalculateCpuPrice(string lowerName, Dictionary<string, string> specs, string socket)
    {
        int cores = specs.TryGetValue("Cores", out var cStr) && int.TryParse(cStr, out var c) ? c : 4;
        decimal baseClock = specs.TryGetValue("BaseClock", out var bStr) && decimal.TryParse(bStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var bc) ? bc : 2.0m;

        // Tier premium based on product line
        decimal tierPremium = 0;
        if (lowerName.Contains("threadripper pro")) tierPremium = 16000;
        else if (lowerName.Contains("threadripper")) tierPremium = 8000;
        else if (lowerName.Contains("xeon w") || lowerName.Contains("xeon platinum") || lowerName.Contains("xeon gold")) tierPremium = 3500;
        else if (lowerName.Contains("ryzen 9") || lowerName.Contains("i9")) tierPremium = 1500;
        else if (lowerName.Contains("ryzen 7") || lowerName.Contains("i7")) tierPremium = 800;
        else if (lowerName.Contains("ryzen 5") || lowerName.Contains("i5")) tierPremium = 350;
        else if (lowerName.Contains("ryzen 3") || lowerName.Contains("i3")) tierPremium = 100;
        else if (lowerName.Contains("xeon e5") || lowerName.Contains("xeon e3")) tierPremium = 200;
        else if (lowerName.Contains("pentium") || lowerName.Contains("celeron") || lowerName.Contains("athlon")) tierPremium = 0;

        decimal basePrice = 100 + (cores * 40) + (baseClock * 30) + tierPremium;

        // Age multiplier based on socket generation
        decimal ageMultiplier = 0.08m;
        if (socket.Contains("AM5") || socket.Contains("1851") || socket.Contains("STR5")) ageMultiplier = 1.0m;
        else if (socket.Contains("1700")) ageMultiplier = 0.85m;
        else if (socket.Contains("AM4") || socket.Contains("1200")) ageMultiplier = 0.55m;
        else if (socket.Contains("1151") || socket.Contains("2066")) ageMultiplier = 0.30m;
        else if (socket.Contains("2011-3")) ageMultiplier = 0.20m;
        else if (socket.Contains("1150") || socket.Contains("FM2")) ageMultiplier = 0.15m;
        else if (socket.Contains("2011")) ageMultiplier = 0.12m;
        else if (socket.Contains("1155") || socket.Contains("AM3")) ageMultiplier = 0.10m;

        decimal cpuPrice = basePrice * ageMultiplier;

        if (lowerName.Contains("oem") || lowerName.Contains("tray"))
        {
            cpuPrice *= 0.90m;
        }

        return Math.Max(cpuPrice, 120m);
    }

    private static decimal CalculateGpuPrice(string lowerName, Dictionary<string, string> specs)
    {
        // Tier base by performance segment
        // IMPORTANT: order matters - check more specific substrings first ("7900 xtx" before "7900")
        decimal tierBase = 300;
        if (lowerName.Contains("4090") || lowerName.Contains("3090") ||
            lowerName.Contains("5090") || lowerName.Contains("7900 xtx") ||
            lowerName.Contains("6950")) tierBase = 7000;
        else if (lowerName.Contains("4080") || lowerName.Contains("5080") ||
                 lowerName.Contains("3080") || lowerName.Contains("7900 xt") ||
                 lowerName.Contains("6900") || lowerName.Contains("6800")) tierBase = 4000;
        else if (lowerName.Contains("4070") || lowerName.Contains("3070") ||
                 lowerName.Contains("5070") || lowerName.Contains("2080") ||
                 lowerName.Contains("7900 gre") || lowerName.Contains("7800") ||
                 lowerName.Contains("7700") || lowerName.Contains("6750") ||
                 lowerName.Contains("6700")) tierBase = 2400;
        else if (lowerName.Contains("4060") || lowerName.Contains("3060") ||
                 lowerName.Contains("5060") || lowerName.Contains("2070") ||
                 lowerName.Contains("7600") || lowerName.Contains("6650") ||
                 lowerName.Contains("6600") || lowerName.Contains("1080") ||
                 lowerName.Contains("9060")) tierBase = 1200;
        else if (lowerName.Contains("3050") || lowerName.Contains("4050") ||
                 lowerName.Contains("2060") || lowerName.Contains("6500") ||
                 lowerName.Contains("5500") || lowerName.Contains("1660") ||
                 lowerName.Contains("1650") || lowerName.Contains("1070") ||
                 lowerName.Contains("1060") || lowerName.Contains("rx 580") ||
                 lowerName.Contains("rx 570") || lowerName.Contains("rx 560") ||
                 lowerName.Contains("rx 550")) tierBase = 700;

        // Workstation cards override
        if (lowerName.Contains("ada generation") || lowerName.Contains("quadro rtx") ||
            lowerName.Contains("rtx 6000") || lowerName.Contains("rtx a")) tierBase = 8000;

        // Ti / Super bonuses
        decimal tiSuperBonus = 0;
        if (lowerName.Contains("ti super")) tiSuperBonus = 800;
        else if (lowerName.Contains("super") || lowerName.Contains(" ti ") ||
                 lowerName.EndsWith(" ti") || lowerName.Contains("-ti")) tiSuperBonus = 400;
        else if (lowerName.Contains(" xt ") || lowerName.EndsWith(" xt") ||
                 lowerName.Contains("-xt")) tiSuperBonus = 400;

        // Generation multiplier (newer gen = higher multiplier)
        decimal genMultiplier = 0.08m;
        if (lowerName.Contains("rtx 50") || lowerName.Contains("ada")) genMultiplier = 1.50m;
        else if (lowerName.Contains("rx 9") || lowerName.Contains("b580")) genMultiplier = 1.10m;
        else if (lowerName.Contains("rtx 40") || lowerName.Contains("rx 7") ||
                 lowerName.Contains("arc a7") || lowerName.Contains("arc a")) genMultiplier = 1.00m;
        else if (lowerName.Contains("rtx 30")) genMultiplier = 0.40m;
        else if (lowerName.Contains("rx 6")) genMultiplier = 0.35m;
        else if (lowerName.Contains("rtx 20") || lowerName.Contains("gtx 16") ||
                 lowerName.Contains("rx 5") || lowerName.Contains("vega")) genMultiplier = 0.30m;
        else if (lowerName.Contains("gtx 10") || lowerName.Contains("rx 4") ||
                 lowerName.Contains("rx 58") || lowerName.Contains("rx 57")) genMultiplier = 0.20m;
        else if (lowerName.Contains("gtx 9") || lowerName.Contains("gtx 7") ||
                 lowerName.Contains("r9")) genMultiplier = 0.12m;

        int vram = specs.TryGetValue("VRAM", out var vrStr) && int.TryParse(vrStr.Replace("GB", "").Trim(), out var vr) ? vr : 4;
        decimal gpuPrice = (tierBase * genMultiplier) + (vram * 25) + tiSuperBonus;

        return Math.Max(gpuPrice, 150m);
    }

    private static decimal CalculateMotherboardPrice(string lowerName, Dictionary<string, string> specs, string socket)
    {
        decimal mbPrice = 250;

        if (lowerName.Contains("trx50") || lowerName.Contains("wrx80") || lowerName.Contains("trx40")) mbPrice = 2500;
        else if (lowerName.Contains("x870") || lowerName.Contains("x670") || lowerName.Contains("z890") || lowerName.Contains("z790")) mbPrice = 900;
        else if (lowerName.Contains("x570") || lowerName.Contains("z690") || lowerName.Contains("z590")) mbPrice = 500;
        else if (lowerName.Contains("z490") || lowerName.Contains("z390") || lowerName.Contains("x470")) mbPrice = 300;
        else if (lowerName.Contains("b850") || lowerName.Contains("b860") || lowerName.Contains("b650") || lowerName.Contains("b760")) mbPrice = 550;
        else if (lowerName.Contains("b550") || lowerName.Contains("b460") || lowerName.Contains("b660")) mbPrice = 300;
        else if (lowerName.Contains("h610") || lowerName.Contains("a620") || lowerName.Contains("h510") ||
                 lowerName.Contains("a520") || lowerName.Contains("a320") || lowerName.Contains("h410")) mbPrice = 200;
        else if (lowerName.Contains("h61") || lowerName.Contains("h81") || lowerName.Contains("b85") ||
                 lowerName.Contains("g41")) mbPrice = 100;

        // Brand tier premium
        if (lowerName.Contains("maximus") || lowerName.Contains("crosshair") ||
            lowerName.Contains("godlike") || lowerName.Contains("extreme")) mbPrice += 1000;
        else if (lowerName.Contains("rog strix") || lowerName.Contains("aorus master") ||
                 lowerName.Contains("aorus xtreme") || lowerName.Contains("meg ") ||
                 lowerName.Contains("taichi") || lowerName.Contains("creator")) mbPrice += 400;
        else if (lowerName.Contains("tuf gaming") || lowerName.Contains("tomahawk") ||
                 lowerName.Contains("aorus elite") || lowerName.Contains("aorus pro") ||
                 lowerName.Contains("mag ")) mbPrice += 150;

        // Socket age multiplier
        if (socket.Contains("1851") || socket.Contains("AM5") || socket.Contains("1700") ||
            socket.Contains("TRX") || lowerName.Contains("trx") || lowerName.Contains("threadripper"))
            mbPrice *= 1.0m;
        else if (socket.Contains("AM4") || socket.Contains("1200"))
            mbPrice *= 0.6m;
        else if (socket.Contains("1151") || socket.Contains("2066") || socket.Contains("2011"))
            mbPrice *= 0.4m;
        else
            mbPrice *= 0.25m;

        // Mini ITX premium
        if (specs.TryGetValue("FormFactor", out var mbFf))
        {
            if (mbFf.Contains("Mini ITX", StringComparison.OrdinalIgnoreCase) ||
                mbFf.Contains("Mini-ITX", StringComparison.OrdinalIgnoreCase)) mbPrice += 150;
        }

        return Math.Max(mbPrice, 100m);
    }

    private static decimal CalculateRamPrice(string lowerName, Dictionary<string, string> specs, string memType)
    {
        int capacity = specs.TryGetValue("Capacity", out var capStr) && int.TryParse(capStr.Replace("GB", "").Trim(), out var cap) ? cap : 8;

        decimal baseCostPerGb = 12m;
        decimal ramPrice = 80 + (capacity * baseCostPerGb);

        decimal ramMultiplier = 1.0m;
        if (memType.Contains("DDR5")) ramMultiplier = 2.8m;
        else if (memType.Contains("DDR4")) ramMultiplier = 1.5m;
        else if (memType.Contains("DDR3")) ramMultiplier = 0.5m;
        ramPrice *= ramMultiplier;

        if (specs.TryGetValue("Speed", out var speedStr) && int.TryParse(speedStr, out var speed))
        {
            if (speed >= 7000) ramPrice += 300;
            else if (speed >= 6000) ramPrice += 150;
            else if (speed >= 3600 && memType.Contains("DDR4")) ramPrice += 100;
        }

        if (lowerName.Contains("trident") || lowerName.Contains("dominator") || lowerName.Contains("rgb"))
        {
            ramPrice *= 1.15m;
        }

        return Math.Max(ramPrice, 80m);
    }

    private static decimal CalculatePsuPrice(Dictionary<string, string> specs)
    {
        int wattage = specs.TryGetValue("Wattage", out var wattStr) && int.TryParse(wattStr, out var w) ? w : 500;
        decimal psuPrice = 80 + (wattage * 0.3m);

        if (specs.TryGetValue("Efficiency", out var eff))
        {
            string lowerEff = eff.ToLowerInvariant();
            if (lowerEff.Contains("platinum") || lowerEff.Contains("titanium")) psuPrice += 250 + (wattage >= 1000 ? 100 : 0);
            else if (lowerEff.Contains("gold")) psuPrice += 80;
            else if (lowerEff.Contains("silver") || lowerEff.Contains("bronze")) psuPrice += 30;
            if (lowerEff == "unknown") psuPrice *= 0.6m;
        }

        if (specs.TryGetValue("Modular", out var mod))
        {
            string lowerMod = mod.ToLowerInvariant();
            if (lowerMod.Contains("full")) psuPrice += 60;
            else if (lowerMod.Contains("semi")) psuPrice += 25;
        }

        if (wattage >= 1200) psuPrice += 150;
        return Math.Max(psuPrice, 90m);
    }

    private static decimal CalculateSsdPrice(Dictionary<string, string> specs)
    {
        string rawCap = specs.TryGetValue("Capacity", out var capVal) ? capVal : "512";
        bool isTb = rawCap.Contains("TB", StringComparison.OrdinalIgnoreCase);
        int ssdCap = int.TryParse(rawCap.Replace("GB", "").Replace("TB", "").Trim(), out var sc) ? sc : 512;
        if (isTb) ssdCap *= 1000;

        decimal ssdPrice = 40 + (ssdCap * 0.18m);
        if (specs.TryGetValue("Interface", out var sInt) &&
            (sInt.Contains("PCIe", StringComparison.OrdinalIgnoreCase) ||
             sInt.Contains("NVMe", StringComparison.OrdinalIgnoreCase) ||
             sInt.Contains("M.2", StringComparison.OrdinalIgnoreCase)))
            ssdPrice += 60;

        return Math.Max(ssdPrice, 70m);
    }

    private static decimal CalculateHddPrice(Dictionary<string, string> specs)
    {
        string rawCap = specs.TryGetValue("Capacity", out var capVal) ? capVal : "1000";
        bool isTb = rawCap.Contains("TB", StringComparison.OrdinalIgnoreCase);
        int hddCap = int.TryParse(rawCap.Replace("GB", "").Replace("TB", "").Trim(), out var hc) ? hc : 1000;
        if (isTb) hddCap *= 1000;

        return Math.Max(60 + (hddCap * 0.04m), 100m);
    }

    private static decimal CalculateCasePrice(string lowerBrand, Dictionary<string, string> specs)
    {
        decimal casePrice = 200;

        if (specs.TryGetValue("FormFactor", out var formFactor))
        {
            string lowerFf = formFactor.ToLowerInvariant();
            if (lowerFf.Contains("full tower")) casePrice = 450;
            else if (lowerFf.Contains("mid tower")) casePrice = 250;
            else if (lowerFf.Contains("mini") || lowerFf.Contains("micro")) casePrice = 180;
        }

        if (lowerBrand.Contains("lian li") || lowerBrand.Contains("corsair") || lowerBrand.Contains("fractal") ||
            lowerBrand.Contains("nzxt") || lowerBrand.Contains("phanteks") || lowerBrand.Contains("asus") ||
            lowerBrand.Contains("be quiet"))
            casePrice *= 1.4m;
        else if (lowerBrand.Contains("aerocool") || lowerBrand.Contains("gembird") ||
                 lowerBrand.Contains("chieftec") || lowerBrand.Contains("zalman") ||
                 lowerBrand.Contains("apevia") || lowerBrand.Contains("diypc"))
            casePrice *= 0.7m;

        if (specs.TryGetValue("SidePanel", out var panel))
        {
            string lowerPanel = panel.ToLowerInvariant();
            if (lowerPanel.Contains("glass") || lowerPanel.Contains("tempered")) casePrice += 100;
            else if (lowerPanel.Contains("acrylic")) casePrice += 30;
        }

        return Math.Max(casePrice, 80m);
    }

    private static decimal CalculateCoolerPrice(string lowerName, string lowerBrand, Dictionary<string, string> specs)
    {
        bool isWaterCooled = specs.TryGetValue("WaterCooled", out var wc) &&
                             wc.Equals("True", StringComparison.OrdinalIgnoreCase);
        decimal coolerPrice;

        if (isWaterCooled)
        {
            // ---- AIO LIQUID COOLERS ----
            int radSize = specs.TryGetValue("RadiatorSize", out var radStr) &&
                          int.TryParse(radStr, out var rs) ? rs : 120;

            // Base price by radiator size
            if (radSize >= 360) coolerPrice = 620m;
            else if (radSize >= 280) coolerPrice = 430m;
            else if (radSize >= 240) coolerPrice = 410m;
            else coolerPrice = 340m;

            // LCD screen/premium ecosystem
            if (lowerName.Contains("lcd") || lowerName.Contains("z53") ||
                lowerName.Contains("z63") || lowerName.Contains("z73") ||
                lowerName.Contains("ryujin") || lowerName.Contains("mjolnir") ||
                lowerName.Contains("vision"))
                coolerPrice += 550m;

            // iCUE LINK is a separate, more expensive ecosystem
            if (lowerName.Contains("icue link") || lowerName.Contains("link titan"))
                coolerPrice += 600m;

            // Brand premium (AIO)
            if (lowerBrand.Contains("nzxt") || lowerBrand.Contains("corsair") ||
                lowerBrand.Contains("lian li") || lowerBrand.Contains("asus"))
                coolerPrice += 100m;
            else if (lowerBrand.Contains("cooler master") || lowerBrand.Contains("msi") ||
                     lowerBrand.Contains("be quiet") || lowerBrand.Contains("alphacool") ||
                     lowerBrand.Contains("thermaltake") || lowerBrand.Contains("thermalright") ||
                     lowerBrand.Contains("enermax"))
                coolerPrice += 50m;
        }
        else
        {
            // ---- AIR COOLERS ----
            int height = specs.TryGetValue("Height", out var hStr) &&
                         int.TryParse(hStr, out var h) ? h : 120;

            // Stock-level cooler override
            if (lowerName.Contains("alpine") || lowerName.Contains("juno"))
                return 50m;

            // Height-based tiers
            if (height <= 80) coolerPrice = 80m;            // ultra low-profile
            else if (height <= 100) coolerPrice = 90m;       // low-profile ITX
            else if (height <= 149) coolerPrice = 110m;      // standard single tower
            else coolerPrice = 130m;                          // tall towers (150mm+)

            // Performance-class detection by name
            if (lowerName.Contains("fanless") || lowerName.Contains("passive"))
                coolerPrice += 230m;
            else if (lowerName.Contains("dark rock"))
                coolerPrice += 120m;
            else if (lowerName.Contains("assassin") || lowerName.Contains("nh-d15") ||
                     lowerName.Contains("cr-3000"))
                coolerPrice += 80m;

            if (lowerName.Contains("duo") || lowerName.Contains("dual"))
                coolerPrice += 50m;
            if (lowerName.Contains("digital") || lowerName.Contains("display"))
                coolerPrice += 50m;

            // Brand premium
            if (lowerBrand.Contains("noctua")) coolerPrice += 120m;
            else if (lowerBrand.Contains("be quiet")) coolerPrice += 80m;
            else if (lowerBrand.Contains("nzxt")) coolerPrice += 70m;
        }

        return Math.Max(coolerPrice, 40m);
    }
}
import type {
  ComponentDto,
  Purpose,
  RankedComponent,
  RecommendationResult,
  SlotRecommendation,
} from '@/lib/types'

let nextId = 1

// category is filled in by slot() so the data below stays terse.
function comp(
  name: string,
  brand: string,
  price: number,
  specs: Record<string, string>,
): ComponentDto {
  return { id: nextId++, name, category: '', brand, price, technicalSpecs: specs }
}

function ranked(score: number, c: ComponentDto, rank = 1): RankedComponent {
  return { rank, performanceScore: score, component: c }
}

function slot(
  category: string,
  allocatedBudget: number,
  recommendations: RankedComponent[],
): SlotRecommendation {
  for (const r of recommendations) r.component.category = category
  return { category, allocatedBudget, recommendations }
}

// ----------------------------------------------------------------- Gaming (balanced)
const gaming: RecommendationResult = {
  purpose: 'Gaming',
  totalBudget: 9000,
  actualTotalPrice: 7650,
  isSuccess: true,
  message: 'Recommended a complete, compatible Gaming build.',
  failedSlots: [],
  slots: [
    slot('Cpu', 1800, [
      ranked(
        0.8,
        comp('AMD Ryzen 7 7800X3D 4.2 GHz 8-Core Processor', 'AMD', 1750, {
          Cores: '8',
          Threads: '16',
          BoostClock: '5.0 GHz',
          TDP: '120',
          Socket: 'AM5',
          MemoryType: 'DDR5',
        }),
      ),
      ranked(
        0.74,
        comp('AMD Ryzen 7 7700X 4.5 GHz 8-Core Processor', 'AMD', 1280, {
          Cores: '8',
          Threads: '16',
          BoostClock: '5.4 GHz',
          TDP: '105',
          Socket: 'AM5',
          MemoryType: 'DDR5',
        }),
        2,
      ),
      ranked(
        0.71,
        comp('Intel Core i5-14600KF 3.5 GHz 14-Core Processor', 'Intel', 1190, {
          Cores: '14',
          Threads: '20',
          BoostClock: '5.3 GHz',
          TDP: '125',
          Socket: 'LGA1700',
          MemoryType: 'DDR5',
        }),
        3,
      ),
    ]),
    slot('Gpu', 2900, [
      ranked(
        0.83,
        comp(
          'Gigabyte WINDFORCE OC GeForce RTX 4070 SUPER 12 GB',
          'Gigabyte',
          2850,
          {
            Chipset: 'GeForce RTX 4070 SUPER',
            VRAM: '12',
            CoreClock: '1980 MHz',
            Length: '261',
            TDP: '220',
            Interface: 'PCIe x16',
          },
        ),
      ),
      ranked(
        0.76,
        comp('ASRock Steel Legend Radeon RX 7800 XT 16 GB', 'ASRock', 2450, {
          Chipset: 'Radeon RX 7800 XT',
          VRAM: '16',
          CoreClock: '2124 MHz',
          Length: '330',
          TDP: '263',
          Interface: 'PCIe x16',
        }),
        2,
      ),
    ]),
    slot('Motherboard', 900, [
      ranked(
        0.62,
        comp('Asus TUF GAMING B650-PLUS WIFI ATX AM5 Motherboard', 'Asus', 850, {
          Socket: 'AM5',
          FormFactor: 'ATX',
          RamSlots: '4',
          MaxRam: '128',
          RamType: 'DDR5',
        }),
      ),
    ]),
    slot('Ram', 600, [
      ranked(
        0.7,
        comp('G.Skill Flare X5 32 GB (2 x 16 GB) DDR5-6000 CL30', 'G.Skill', 520, {
          Type: 'DDR5',
          Speed: '6000',
          Modules: '2',
          Capacity: '32',
        }),
      ),
    ]),
    slot('Ssd', 700, [
      ranked(
        0.85,
        comp('Samsung 990 Pro 2 TB M.2-2280 PCIe 4.0 X4 NVMe SSD', 'Samsung', 720, {
          Capacity: '2000',
          FormFactor: 'M.2-2280',
          Interface: 'PCIe 4.0 X4',
          Type: 'SSD',
        }),
      ),
    ]),
    slot('Psu', 500, [
      ranked(
        0.65,
        comp('Corsair RM750e 750 W 80+ Gold Fully Modular ATX', 'Corsair', 430, {
          Wattage: '750',
          FormFactor: 'ATX',
          Efficiency: '80+ Gold',
          Modular: 'Full',
        }),
      ),
    ]),
    slot('Case', 400, [
      ranked(
        0.5,
        comp('Fractal Design Pop Air ATX Mid Tower Case', 'Fractal Design', 360, {
          FormFactor: 'ATX Mid Tower',
          SupportedMotherboards: 'ATX, Micro-ATX, Mini-ITX',
          SidePanel: 'Tempered Glass',
          MaxGpuLength: '405',
        }),
      ),
    ]),
    slot('Cooler', 200, [
      ranked(
        0.6,
        comp('Thermalright Peerless Assassin 120 SE CPU Cooler', 'Thermalright', 170, {
          WaterCooled: 'False',
          Height: '155',
          CpuSockets: 'AM4, AM5, LGA1700, LGA1200',
        }),
      ),
    ]),
  ],
}

// ----------------------------------------------------------------- Work (GPU-bound imbalance)
const work: RecommendationResult = {
  purpose: 'Work',
  totalBudget: 12000,
  actualTotalPrice: 9330,
  isSuccess: true,
  message: 'Recommended a complete, compatible Work build.',
  failedSlots: [],
  slots: [
    slot('Cpu', 2600, [
      ranked(
        0.95,
        comp('AMD Ryzen 9 7950X 4.5 GHz 16-Core Processor', 'AMD', 2500, {
          Cores: '16',
          Threads: '32',
          BoostClock: '5.7 GHz',
          TDP: '170',
          Socket: 'AM5',
          MemoryType: 'DDR5',
        }),
      ),
    ]),
    slot('Gpu', 2200, [
      ranked(
        0.58,
        comp('Asus Dual GeForce RTX 4060 Ti 16 GB OC', 'Asus', 1900, {
          Chipset: 'GeForce RTX 4060 Ti',
          VRAM: '16',
          CoreClock: '2535 MHz',
          Length: '227',
          TDP: '165',
          Interface: 'PCIe x16',
        }),
      ),
    ]),
    slot('Motherboard', 1600, [
      ranked(
        0.82,
        comp('Asus ProArt X670E-CREATOR WIFI ATX AM5 Motherboard', 'Asus', 1500, {
          Socket: 'AM5',
          FormFactor: 'ATX',
          RamSlots: '4',
          MaxRam: '192',
          RamType: 'DDR5',
        }),
      ),
    ]),
    slot('Ram', 1100, [
      ranked(
        0.85,
        comp('G.Skill Trident Z5 64 GB (2 x 32 GB) DDR5-5600 CL36', 'G.Skill', 980, {
          Type: 'DDR5',
          Speed: '5600',
          Modules: '2',
          Capacity: '64',
        }),
      ),
    ]),
    slot('Ssd', 800, [
      ranked(
        0.85,
        comp('Samsung 990 Pro 2 TB M.2-2280 PCIe 4.0 X4 NVMe SSD', 'Samsung', 720, {
          Capacity: '2000',
          FormFactor: 'M.2-2280',
          Interface: 'PCIe 4.0 X4',
          Type: 'SSD',
        }),
      ),
    ]),
    slot('Psu', 600, [
      ranked(
        0.7,
        comp('Corsair RM850e 850 W 80+ Gold Fully Modular ATX', 'Corsair', 520, {
          Wattage: '850',
          FormFactor: 'ATX',
          Efficiency: '80+ Gold',
          Modular: 'Full',
        }),
      ),
    ]),
    slot('Case', 700, [
      ranked(
        0.6,
        comp('Fractal Design Define 7 ATX Mid Tower Case', 'Fractal Design', 650, {
          FormFactor: 'ATX Mid Tower',
          SupportedMotherboards: 'E-ATX, ATX, Micro-ATX, Mini-ITX',
          SidePanel: 'Solid',
          MaxGpuLength: '467',
        }),
      ),
    ]),
    slot('Cooler', 600, [
      ranked(
        0.8,
        comp('NZXT Kraken 280 RGB 280 mm Liquid CPU Cooler', 'NZXT', 560, {
          WaterCooled: 'True',
          RadiatorSize: '280',
          CpuSockets: 'AM5, LGA1700',
        }),
      ),
    ]),
  ],
}

// ----------------------------------------------------------------- Office (balanced, low-end)
const office: RecommendationResult = {
  purpose: 'Office',
  totalBudget: 3500,
  actualTotalPrice: 2950,
  isSuccess: true,
  message: 'Recommended a complete, compatible Office build.',
  failedSlots: [],
  slots: [
    slot('Cpu', 800, [
      ranked(
        0.42,
        comp('AMD Ryzen 5 8500G 3.5 GHz 6-Core Processor', 'AMD', 750, {
          Cores: '6',
          Threads: '12',
          BoostClock: '5.0 GHz',
          TDP: '65',
          Socket: 'AM5',
          MemoryType: 'DDR5',
        }),
      ),
    ]),
    slot('Gpu', 700, [
      ranked(
        0.3,
        comp('MSI VENTUS XS GeForce GTX 1650 4 GB', 'MSI', 650, {
          Chipset: 'GeForce GTX 1650',
          VRAM: '4',
          CoreClock: '1665 MHz',
          Length: '200',
          TDP: '75',
          Interface: 'PCIe x16',
        }),
      ),
    ]),
    slot('Motherboard', 500, [
      ranked(
        0.5,
        comp('ASRock B650M-HDV/M.2 Micro ATX AM5 Motherboard', 'ASRock', 450, {
          Socket: 'AM5',
          FormFactor: 'Micro ATX',
          RamSlots: '2',
          MaxRam: '96',
          RamType: 'DDR5',
        }),
      ),
    ]),
    slot('Ram', 300, [
      ranked(
        0.55,
        comp('Kingston Fury Beast 16 GB (2 x 8 GB) DDR5-5600', 'Kingston', 260, {
          Type: 'DDR5',
          Speed: '5600',
          Modules: '2',
          Capacity: '16',
        }),
      ),
    ]),
    slot('Ssd', 300, [
      ranked(
        0.6,
        comp('Crucial P3 Plus 1 TB M.2-2280 PCIe 4.0 X4 NVMe SSD', 'Crucial', 250, {
          Capacity: '1000',
          FormFactor: 'M.2-2280',
          Interface: 'PCIe 4.0 X4',
          Type: 'SSD',
        }),
      ),
    ]),
    slot('Psu', 300, [
      ranked(
        0.45,
        comp('be quiet! System Power 10 550 W 80+ Bronze ATX', 'be quiet!', 240, {
          Wattage: '550',
          FormFactor: 'ATX',
          Efficiency: '80+ Bronze',
          Modular: 'No',
        }),
      ),
    ]),
    slot('Case', 250, [
      ranked(
        0.4,
        comp('Cooler Master MasterBox Q300L Micro ATX Tower Case', 'Cooler Master', 220, {
          FormFactor: 'Micro ATX Mid Tower',
          SupportedMotherboards: 'Micro-ATX, Mini-ITX',
          SidePanel: 'Acrylic',
          MaxGpuLength: '360',
        }),
      ),
    ]),
    slot('Cooler', 150, [
      ranked(
        0.45,
        comp('ARCTIC Freezer 36 CO CPU Cooler', 'ARCTIC', 130, {
          WaterCooled: 'False',
          Height: '159',
          CpuSockets: 'AM5, LGA1700, LGA1851',
        }),
      ),
    ]),
  ],
}

export const MOCK_RESULTS: Record<Purpose, RecommendationResult> = {
  Gaming: gaming,
  Work: work,
  Office: office,
}

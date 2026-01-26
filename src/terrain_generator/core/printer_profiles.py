"""
Bambu Labs Printer Profiles
Configuration profiles for all Bambu Labs printers including P1S, P2S, X1C, A1, etc.
"""

from dataclasses import dataclass
from typing import Dict, List, Optional, Tuple


@dataclass
class PrinterProfile:
    """Configuration profile for a 3D printer."""
    name: str
    model: str
    manufacturer: str
    
    # Build volume (mm)
    build_width: float
    build_depth: float
    build_height: float
    
    # Print capabilities
    max_print_speed: float  # mm/s
    max_travel_speed: float  # mm/s
    heated_bed: bool
    max_bed_temp: float  # °C
    max_nozzle_temp: float  # °C
    
    # Multi-material capabilities
    has_ams: bool  # Automatic Material System
    ams_slots: int
    max_colors: int
    
    # File format support
    supported_formats: List[str]
    native_format: str
    
    # Recommended settings for terrain maps
    recommended_layer_height: float  # mm
    recommended_infill: int  # percentage
    recommended_wall_thickness: float  # mm
    
    # Material recommendations
    recommended_materials: List[str]
    
    def get_max_terrain_size(self, margin: float = 5.0) -> Tuple[float, float]:
        """Get maximum terrain dimensions accounting for margins."""
        return (
            self.build_width - 2 * margin,
            self.build_depth - 2 * margin
        )
        
    def validate_dimensions(self, width: float, depth: float, height: float) -> Tuple[bool, str]:
        """Check if dimensions fit within build volume."""
        if width > self.build_width:
            return False, f"Width {width}mm exceeds build width {self.build_width}mm"
        if depth > self.build_depth:
            return False, f"Depth {depth}mm exceeds build depth {self.build_depth}mm"
        if height > self.build_height:
            return False, f"Height {height}mm exceeds build height {self.build_height}mm"
        return True, "Dimensions OK"


# Bambu Labs Printer Profiles
BAMBU_PRINTERS: Dict[str, PrinterProfile] = {
    "X1C": PrinterProfile(
        name="Bambu Lab X1 Carbon",
        model="X1C",
        manufacturer="Bambu Lab",
        build_width=256,
        build_depth=256,
        build_height=256,
        max_print_speed=500,
        max_travel_speed=700,
        heated_bed=True,
        max_bed_temp=110,
        max_nozzle_temp=300,
        has_ams=True,
        ams_slots=4,
        max_colors=16,  # With 4 AMS units
        supported_formats=["3mf", "stl", "step", "obj"],
        native_format="3mf",
        recommended_layer_height=0.16,
        recommended_infill=15,
        recommended_wall_thickness=0.8,
        recommended_materials=["PLA", "PETG", "ABS", "ASA", "TPU", "PLA-CF", "PA-CF"]
    ),
    
    "X1E": PrinterProfile(
        name="Bambu Lab X1E",
        model="X1E",
        manufacturer="Bambu Lab",
        build_width=256,
        build_depth=256,
        build_height=256,
        max_print_speed=500,
        max_travel_speed=700,
        heated_bed=True,
        max_bed_temp=120,
        max_nozzle_temp=320,
        has_ams=True,
        ams_slots=4,
        max_colors=16,
        supported_formats=["3mf", "stl", "step", "obj"],
        native_format="3mf",
        recommended_layer_height=0.16,
        recommended_infill=15,
        recommended_wall_thickness=0.8,
        recommended_materials=["PLA", "PETG", "ABS", "ASA", "PA", "PC", "PPS", "PEEK"]
    ),
    
    "P1P": PrinterProfile(
        name="Bambu Lab P1P",
        model="P1P",
        manufacturer="Bambu Lab",
        build_width=256,
        build_depth=256,
        build_height=256,
        max_print_speed=500,
        max_travel_speed=500,
        heated_bed=True,
        max_bed_temp=100,
        max_nozzle_temp=300,
        has_ams=True,
        ams_slots=4,
        max_colors=4,
        supported_formats=["3mf", "stl", "obj"],
        native_format="3mf",
        recommended_layer_height=0.20,
        recommended_infill=15,
        recommended_wall_thickness=0.8,
        recommended_materials=["PLA", "PETG", "TPU"]
    ),
    
    "P1S": PrinterProfile(
        name="Bambu Lab P1S",
        model="P1S",
        manufacturer="Bambu Lab",
        build_width=256,
        build_depth=256,
        build_height=256,
        max_print_speed=500,
        max_travel_speed=500,
        heated_bed=True,
        max_bed_temp=100,
        max_nozzle_temp=300,
        has_ams=True,
        ams_slots=4,
        max_colors=4,
        supported_formats=["3mf", "stl", "obj"],
        native_format="3mf",
        recommended_layer_height=0.16,
        recommended_infill=15,
        recommended_wall_thickness=0.8,
        recommended_materials=["PLA", "PETG", "ABS", "ASA", "TPU"]
    ),
    
    # Bambu Lab P2S - New Model with enhanced capabilities
    "P2S": PrinterProfile(
        name="Bambu Lab P2S",
        model="P2S",
        manufacturer="Bambu Lab",
        build_width=256,
        build_depth=256,
        build_height=256,
        max_print_speed=600,  # Upgraded speed
        max_travel_speed=600,
        heated_bed=True,
        max_bed_temp=110,
        max_nozzle_temp=300,
        has_ams=True,
        ams_slots=4,
        max_colors=16,  # Multi-AMS support
        supported_formats=["3mf", "stl", "step", "obj"],
        native_format="3mf",
        recommended_layer_height=0.16,
        recommended_infill=15,
        recommended_wall_thickness=0.8,
        recommended_materials=["PLA", "PETG", "ABS", "ASA", "TPU", "PLA-CF"]
    ),
    
    "A1": PrinterProfile(
        name="Bambu Lab A1",
        model="A1",
        manufacturer="Bambu Lab",
        build_width=256,
        build_depth=256,
        build_height=256,
        max_print_speed=500,
        max_travel_speed=500,
        heated_bed=True,
        max_bed_temp=100,
        max_nozzle_temp=300,
        has_ams=True,
        ams_slots=4,
        max_colors=4,
        supported_formats=["3mf", "stl", "obj"],
        native_format="3mf",
        recommended_layer_height=0.20,
        recommended_infill=15,
        recommended_wall_thickness=0.8,
        recommended_materials=["PLA", "PETG", "TPU"]
    ),
    
    "A1_MINI": PrinterProfile(
        name="Bambu Lab A1 mini",
        model="A1_MINI",
        manufacturer="Bambu Lab",
        build_width=180,
        build_depth=180,
        build_height=180,
        max_print_speed=500,
        max_travel_speed=500,
        heated_bed=True,
        max_bed_temp=80,
        max_nozzle_temp=300,
        has_ams=True,
        ams_slots=4,
        max_colors=4,
        supported_formats=["3mf", "stl", "obj"],
        native_format="3mf",
        recommended_layer_height=0.20,
        recommended_infill=15,
        recommended_wall_thickness=0.8,
        recommended_materials=["PLA", "PETG", "TPU"]
    ),
}


def get_printer_profile(model: str) -> Optional[PrinterProfile]:
    """Get printer profile by model name."""
    return BAMBU_PRINTERS.get(model.upper())


def get_all_printers() -> Dict[str, PrinterProfile]:
    """Get all available printer profiles."""
    return BAMBU_PRINTERS.copy()


def get_printer_names() -> List[str]:
    """Get list of all printer model names."""
    return list(BAMBU_PRINTERS.keys())


@dataclass
class MaterialProfile:
    """Material profile for 3D printing."""
    name: str
    type: str  # PLA, PETG, ABS, etc.
    
    # Temperature settings
    nozzle_temp_min: float
    nozzle_temp_max: float
    nozzle_temp_default: float
    bed_temp_min: float
    bed_temp_max: float
    bed_temp_default: float
    
    # Print settings
    recommended_speed: float  # mm/s
    cooling_required: bool
    fan_speed: int  # 0-100%
    
    # Physical properties
    density: float  # g/cm³
    
    # Use cases
    suitable_for_terrain: bool
    outdoor_rated: bool
    flexibility: str  # rigid, semi-flexible, flexible
    
    # Appearance
    finish: str  # matte, glossy, textured
    transparency: str  # opaque, translucent, transparent


MATERIAL_PROFILES: Dict[str, MaterialProfile] = {
    "PLA": MaterialProfile(
        name="PLA (Polylactic Acid)",
        type="PLA",
        nozzle_temp_min=190,
        nozzle_temp_max=230,
        nozzle_temp_default=210,
        bed_temp_min=45,
        bed_temp_max=65,
        bed_temp_default=55,
        recommended_speed=100,
        cooling_required=True,
        fan_speed=100,
        density=1.24,
        suitable_for_terrain=True,
        outdoor_rated=False,
        flexibility="rigid",
        finish="matte",
        transparency="opaque"
    ),
    
    "PETG": MaterialProfile(
        name="PETG",
        type="PETG",
        nozzle_temp_min=220,
        nozzle_temp_max=260,
        nozzle_temp_default=240,
        bed_temp_min=70,
        bed_temp_max=85,
        bed_temp_default=75,
        recommended_speed=80,
        cooling_required=True,
        fan_speed=50,
        density=1.27,
        suitable_for_terrain=True,
        outdoor_rated=True,
        flexibility="rigid",
        finish="glossy",
        transparency="translucent"
    ),
    
    "ASA": MaterialProfile(
        name="ASA",
        type="ASA",
        nozzle_temp_min=240,
        nozzle_temp_max=270,
        nozzle_temp_default=255,
        bed_temp_min=90,
        bed_temp_max=110,
        bed_temp_default=100,
        recommended_speed=70,
        cooling_required=False,
        fan_speed=30,
        density=1.07,
        suitable_for_terrain=True,
        outdoor_rated=True,
        flexibility="rigid",
        finish="matte",
        transparency="opaque"
    ),
    
    "WOOD_PLA": MaterialProfile(
        name="Wood Fill PLA",
        type="PLA_COMPOSITE",
        nozzle_temp_min=195,
        nozzle_temp_max=220,
        nozzle_temp_default=210,
        bed_temp_min=50,
        bed_temp_max=65,
        bed_temp_default=55,
        recommended_speed=60,
        cooling_required=True,
        fan_speed=100,
        density=1.30,
        suitable_for_terrain=True,
        outdoor_rated=False,
        flexibility="rigid",
        finish="textured",
        transparency="opaque"
    ),
}


def get_material_profile(name: str) -> Optional[MaterialProfile]:
    """Get material profile by name."""
    return MATERIAL_PROFILES.get(name.upper())


def get_terrain_suitable_materials() -> List[MaterialProfile]:
    """Get materials suitable for terrain printing."""
    return [m for m in MATERIAL_PROFILES.values() if m.suitable_for_terrain]

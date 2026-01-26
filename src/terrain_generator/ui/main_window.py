"""
3D Terrain Map Generator - Main Application
A Windows GUI application for creating 3D-printable terrain maps,
optimized for Bambu Labs printers.
"""

import sys
import numpy as np

from PyQt6.QtWidgets import (
    QApplication, QMainWindow, QWidget, QVBoxLayout, QHBoxLayout,
    QSplitter, QTabWidget, QGroupBox, QLabel, QDoubleSpinBox,
    QSpinBox, QComboBox, QPushButton, QFileDialog, QStatusBar,
    QToolBar, QProgressBar, QMessageBox, QFrame,
    QScrollArea, QFormLayout, QLineEdit, QCompleter
)
from PyQt6.QtCore import Qt, QThread, pyqtSignal, QStringListModel
from PyQt6.QtGui import QAction

# Import our modules using relative imports
from ..core.terrain_processor import TerrainProcessor, TerrainSettings
from ..core.mesh_generator import MeshGenerator, MeshData
from ..core.printer_profiles import (
    get_all_printers, get_printer_profile, PrinterProfile
)
from ..exporters.stl_exporter import STLExporter, ThreeMFExporter
from ..data_sources.elevation_sources import SyntheticElevationSource


class Viewport3D(QFrame):
    """3D viewport for terrain preview using basic rendering."""
    
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setMinimumSize(400, 400)
        self.setFrameStyle(QFrame.Shape.Box | QFrame.Shadow.Sunken)
        self.setStyleSheet("background-color: #1a1a2e;")
        
        layout = QVBoxLayout(self)
        self.info_label = QLabel("3D Preview\n\nLoad terrain data to see preview")
        self.info_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self.info_label.setStyleSheet("color: #888; font-size: 14px;")
        layout.addWidget(self.info_label)
        
        self.mesh_data = None
        self.rotation_x = 30
        self.rotation_y = 45
        
    def set_mesh(self, mesh: MeshData):
        """Set mesh data for rendering."""
        self.mesh_data = mesh
        self.update_display()
        
    def update_display(self):
        """Update the display with current mesh info."""
        if self.mesh_data is not None:
            stats = self.mesh_data.validate()
            dims = self.mesh_data.vertices.max(axis=0) - self.mesh_data.vertices.min(axis=0)
            
            text = f"""3D Preview
            
Mesh Statistics:
• Vertices: {self.mesh_data.vertex_count:,}
• Faces: {self.mesh_data.face_count:,}

Dimensions:
• Width: {dims[0]:.1f} mm
• Depth: {dims[1]:.1f} mm
• Height: {dims[2]:.1f} mm

View Rotation:
• X: {self.rotation_x}°
• Y: {self.rotation_y}°

Mesh Valid: {'✓' if stats[0] else '✗'}"""
            
            self.info_label.setText(text)
            self.info_label.setStyleSheet("color: #4ade80; font-size: 12px;")


class MapSelectionPanel(QWidget):
    """Panel for selecting map location and bounds."""
    
    location_changed = pyqtSignal(tuple)
    
    # Database of known locations for search
    KNOWN_LOCATIONS = {
        "Mount Everest": (27.9881, 86.9250),
        "Grand Canyon": (36.1069, -112.1129),
        "Matterhorn": (45.9766, 7.6586),
        "Mount Fuji": (35.3606, 138.7274),
        "Yosemite Valley": (37.7456, -119.5936),
        "Denali": (63.0695, -151.0074),
        "Mount Kilimanjaro": (-3.0674, 37.3556),
        "Mount Rainier": (46.8523, -121.7603),
        "Mount Hood": (45.3735, -121.6959),
        "Crater Lake": (42.9446, -122.1090),
        "Yellowstone": (44.4280, -110.5885),
        "Zion National Park": (37.2982, -113.0263),
        "Bryce Canyon": (37.5930, -112.1871),
        "Arches National Park": (38.7331, -109.5925),
        "Rocky Mountain National Park": (40.3428, -105.6836),
        "Glacier National Park": (48.7596, -113.7870),
        "Mont Blanc": (45.8326, 6.8652),
        "Swiss Alps": (46.8182, 8.2275),
        "Dolomites": (46.4102, 11.8440),
        "Pyrenees": (42.6500, 1.0000),
        "Scottish Highlands": (57.1200, -4.7100),
        "Norwegian Fjords": (61.5000, 6.0000),
        "Iceland Volcanic": (64.9631, -19.0208),
        "Himalayas": (28.0000, 85.0000),
        "Andes Mountains": (-22.8371, -67.0544),  # Corrected: Andes central region
        "Patagonia": (-50.9423, -73.4068),
        "Mount Cook": (-43.5950, 170.1418),
        "Blue Mountains": (-33.7000, 150.3000),
        "Table Mountain": (-33.9625, 18.4097),
        "Mount Kenya": (-0.1521, 37.3084),
        "Atlas Mountains": (31.0600, -7.9100),
        "Death Valley": (36.5054, -117.0794),
        "Badlands": (43.8554, -102.3397),
        "Aconcagua": (-32.6532, -70.0109),
        "K2": (35.8808, 76.5155),
        "Annapurna": (28.5964, 83.8203),
        "Machu Picchu": (-13.1631, -72.5450),
        "Hawaii Volcanoes": (19.4194, -155.2885),
        "Mount St. Helens": (46.1912, -122.1944),
        "Teide": (28.2723, -16.6424),
        "Mount Etna": (37.7510, 14.9934),
    }
    
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setup_ui()
        
    def setup_ui(self):
        layout = QVBoxLayout(self)
        
        # Location search
        search_group = QGroupBox("Search Location")
        search_layout = QVBoxLayout(search_group)
        
        self.search_input = QLineEdit()
        self.search_input.setPlaceholderText("Type to search locations...")
        
        # Setup autocomplete
        location_names = list(self.KNOWN_LOCATIONS.keys())
        completer = QCompleter(location_names)
        completer.setCaseSensitivity(Qt.CaseSensitivity.CaseInsensitive)
        completer.setFilterMode(Qt.MatchFlag.MatchContains)
        self.search_input.setCompleter(completer)
        
        # Connect search
        self.search_input.returnPressed.connect(self._on_search)
        
        search_layout.addWidget(self.search_input)
        
        search_btn = QPushButton("Search")
        search_btn.clicked.connect(self._on_search)
        search_layout.addWidget(search_btn)
        
        layout.addWidget(search_group)
        
        # Coordinates input
        coords_group = QGroupBox("Location Coordinates")
        coords_layout = QFormLayout(coords_group)
        
        self.lat_spin = QDoubleSpinBox()
        self.lat_spin.setRange(-90, 90)
        self.lat_spin.setDecimals(6)
        self.lat_spin.setValue(45.9766)  # Matterhorn
        self.lat_spin.setSuffix("°")
        coords_layout.addRow("Latitude:", self.lat_spin)
        
        self.lon_spin = QDoubleSpinBox()
        self.lon_spin.setRange(-180, 180)
        self.lon_spin.setDecimals(6)
        self.lon_spin.setValue(7.6586)  # Matterhorn
        self.lon_spin.setSuffix("°")
        coords_layout.addRow("Longitude:", self.lon_spin)
        
        layout.addWidget(coords_group)
        
        # Size selection
        size_group = QGroupBox("Area Selection")
        size_layout = QFormLayout(size_group)
        
        self.area_width = QDoubleSpinBox()
        self.area_width.setRange(1, 1000)
        self.area_width.setValue(10)
        self.area_width.setSuffix(" km")
        size_layout.addRow("Width:", self.area_width)
        
        self.area_height = QDoubleSpinBox()
        self.area_height.setRange(1, 1000)
        self.area_height.setValue(10)
        self.area_height.setSuffix(" km")
        size_layout.addRow("Height:", self.area_height)
        
        layout.addWidget(size_group)
        
        # Quick locations
        quick_group = QGroupBox("Quick Locations")
        quick_layout = QVBoxLayout(quick_group)
        
        quick_locations = [
            ("Mount Everest", 27.9881, 86.9250),
            ("Grand Canyon", 36.1069, -112.1129),
            ("Matterhorn", 45.9766, 7.6586),
            ("Mount Fuji", 35.3606, 138.7274),
            ("Yosemite Valley", 37.7456, -119.5936),
        ]
        
        for name, lat, lon in quick_locations:
            btn = QPushButton(name)
            btn.clicked.connect(lambda checked, la=lat, lo=lon: self.set_location(la, lo))
            quick_layout.addWidget(btn)
            
        layout.addWidget(quick_group)
        layout.addStretch()
        
    def _on_search(self):
        """Handle search input."""
        search_text = self.search_input.text().strip()
        if not search_text:
            return
            
        # Try to find exact match first (case-insensitive)
        for name, (lat, lon) in self.KNOWN_LOCATIONS.items():
            if name.lower() == search_text.lower():
                self.set_location(lat, lon)
                self.search_input.clear()
                return
                
        # Try partial match
        for name, (lat, lon) in self.KNOWN_LOCATIONS.items():
            if search_text.lower() in name.lower():
                self.set_location(lat, lon)
                self.search_input.clear()
                return
                
        # No match found - show message
        QMessageBox.information(
            self, "Location Not Found",
            f"Location '{search_text}' not found in database.\n\n"
            "You can manually enter coordinates or select from Quick Locations."
        )
        
    def set_location(self, lat: float, lon: float):
        """Set the location coordinates."""
        self.lat_spin.setValue(lat)
        self.lon_spin.setValue(lon)
        self.location_changed.emit((lat, lon))
        
    def get_bounds(self):
        """Get the current bounds as (min_lon, min_lat, max_lon, max_lat)."""
        lat = self.lat_spin.value()
        lon = self.lon_spin.value()
        
        # Convert km to degrees (approximate)
        km_per_deg_lat = 111.0
        km_per_deg_lon = 111.0 * np.cos(np.radians(lat))
        
        # Avoid division by zero at polar latitudes where cos(latitude) == 0
        if abs(km_per_deg_lon) < 1e-6:
            width_deg = 0.0
        else:
            width_deg = self.area_width.value() / km_per_deg_lon / 2
        height_deg = self.area_height.value() / km_per_deg_lat / 2
        
        return (
            lon - width_deg,
            lat - height_deg,
            lon + width_deg,
            lat + height_deg
        )


class TerrainSettingsPanel(QWidget):
    """Panel for terrain processing settings."""
    
    settings_changed = pyqtSignal(TerrainSettings)
    
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setup_ui()
        
    def setup_ui(self):
        layout = QVBoxLayout(self)
        
        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll_widget = QWidget()
        scroll_layout = QVBoxLayout(scroll_widget)
        
        # Elevation settings
        elev_group = QGroupBox("Elevation")
        elev_layout = QFormLayout(elev_group)
        
        self.exaggeration_spin = QDoubleSpinBox()
        self.exaggeration_spin.setRange(0.5, 20)
        self.exaggeration_spin.setValue(2.0)
        self.exaggeration_spin.setSingleStep(0.5)
        elev_layout.addRow("Vertical Exaggeration:", self.exaggeration_spin)
        
        self.smoothing_spin = QDoubleSpinBox()
        self.smoothing_spin.setRange(0, 10)
        self.smoothing_spin.setValue(1.0)
        self.smoothing_spin.setSingleStep(0.5)
        elev_layout.addRow("Smoothing:", self.smoothing_spin)
        
        scroll_layout.addWidget(elev_group)
        
        # Dimensions
        dim_group = QGroupBox("Dimensions")
        dim_layout = QFormLayout(dim_group)
        
        self.width_spin = QDoubleSpinBox()
        self.width_spin.setRange(50, 500)
        self.width_spin.setValue(150)
        self.width_spin.setSuffix(" mm")
        dim_layout.addRow("Width:", self.width_spin)
        
        self.height_spin = QDoubleSpinBox()
        self.height_spin.setRange(50, 500)
        self.height_spin.setValue(150)
        self.height_spin.setSuffix(" mm")
        dim_layout.addRow("Depth:", self.height_spin)
        
        self.max_height_spin = QDoubleSpinBox()
        self.max_height_spin.setRange(5, 100)
        self.max_height_spin.setValue(30)
        self.max_height_spin.setSuffix(" mm")
        dim_layout.addRow("Max Height:", self.max_height_spin)
        
        scroll_layout.addWidget(dim_group)
        
        # Base settings
        base_group = QGroupBox("Base")
        base_layout = QFormLayout(base_group)
        
        self.base_type_combo = QComboBox()
        self.base_type_combo.addItems(["flat", "tapered", "contoured", "minimal", "floating"])
        base_layout.addRow("Base Type:", self.base_type_combo)
        
        self.base_thickness_spin = QDoubleSpinBox()
        self.base_thickness_spin.setRange(1, 30)
        self.base_thickness_spin.setValue(3)
        self.base_thickness_spin.setSuffix(" mm")
        base_layout.addRow("Thickness:", self.base_thickness_spin)
        
        scroll_layout.addWidget(base_group)
        
        # Resolution
        res_group = QGroupBox("Resolution")
        res_layout = QFormLayout(res_group)
        
        self.resolution_spin = QSpinBox()
        self.resolution_spin.setRange(64, 1024)
        self.resolution_spin.setValue(256)
        self.resolution_spin.setSingleStep(64)
        res_layout.addRow("Mesh Resolution:", self.resolution_spin)
        
        scroll_layout.addWidget(res_group)
        scroll_layout.addStretch()
        
        scroll.setWidget(scroll_widget)
        layout.addWidget(scroll)
        
    def get_settings(self) -> TerrainSettings:
        """Get current terrain settings."""
        return TerrainSettings(
            vertical_exaggeration=self.exaggeration_spin.value(),
            smoothing_radius=self.smoothing_spin.value(),
            mesh_resolution=self.resolution_spin.value(),
            base_type=self.base_type_combo.currentText(),
            base_thickness=self.base_thickness_spin.value(),
            map_width=self.width_spin.value(),
            map_height=self.height_spin.value(),
            max_print_height=self.max_height_spin.value()
        )


class PrinterSettingsPanel(QWidget):
    """Panel for printer selection and settings."""
    
    printer_changed = pyqtSignal(PrinterProfile)
    
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setup_ui()
        
    def setup_ui(self):
        layout = QVBoxLayout(self)
        
        # Printer selection
        printer_group = QGroupBox("Bambu Labs Printer")
        printer_layout = QFormLayout(printer_group)
        
        self.printer_combo = QComboBox()
        printers = get_all_printers()
        for model, profile in printers.items():
            self.printer_combo.addItem(f"{profile.name}", model)
        
        # Select P2S by default as it's mentioned in requirements
        idx = self.printer_combo.findData("P2S")
        if idx >= 0:
            self.printer_combo.setCurrentIndex(idx)
            
        self.printer_combo.currentIndexChanged.connect(self._on_printer_changed)
        printer_layout.addRow("Model:", self.printer_combo)
        
        layout.addWidget(printer_group)
        
        # Printer info
        self.info_group = QGroupBox("Printer Specifications")
        self.info_layout = QFormLayout(self.info_group)
        
        self.build_volume_label = QLabel()
        self.info_layout.addRow("Build Volume:", self.build_volume_label)
        
        self.ams_label = QLabel()
        self.info_layout.addRow("AMS Support:", self.ams_label)
        
        self.max_colors_label = QLabel()
        self.info_layout.addRow("Max Colors:", self.max_colors_label)
        
        self.materials_label = QLabel()
        self.materials_label.setWordWrap(True)
        self.info_layout.addRow("Materials:", self.materials_label)
        
        layout.addWidget(self.info_group)
        
        # Export settings
        export_group = QGroupBox("Export Settings")
        export_layout = QFormLayout(export_group)
        
        self.format_combo = QComboBox()
        self.format_combo.addItems(["STL (Binary)", "STL (ASCII)", "3MF"])
        export_layout.addRow("Format:", self.format_combo)
        
        layout.addWidget(export_group)
        layout.addStretch()
        
        self._update_printer_info()
        
    def _on_printer_changed(self):
        self._update_printer_info()
        profile = self.get_current_printer()
        if profile:
            self.printer_changed.emit(profile)
            
    def _update_printer_info(self):
        profile = self.get_current_printer()
        if profile:
            self.build_volume_label.setText(
                f"{profile.build_width} × {profile.build_depth} × {profile.build_height} mm"
            )
            self.ams_label.setText("Yes" if profile.has_ams else "No")
            self.max_colors_label.setText(str(profile.max_colors))
            self.materials_label.setText(", ".join(profile.recommended_materials[:5]))
            
    def get_current_printer(self) -> PrinterProfile:
        """Get the currently selected printer profile."""
        model = self.printer_combo.currentData()
        return get_printer_profile(model)


class GenerateMeshWorker(QThread):
    """Worker thread for mesh generation."""
    
    progress = pyqtSignal(int, str)
    finished = pyqtSignal(MeshData)
    error = pyqtSignal(str)
    
    def __init__(self, bounds, settings: TerrainSettings):
        super().__init__()
        self.bounds = bounds
        self.settings = settings
        
    def run(self):
        try:
            self.progress.emit(10, "Generating terrain data...")
            
            # Generate a unique seed based on location bounds to get different terrain
            # for different locations (avoiding the fixed seed=42 that caused repeating terrain)
            # Use hash() on the bounds tuple for better distribution and to avoid collisions
            seed = abs(hash(self.bounds)) % (2**31)
            
            source = SyntheticElevationSource(seed=seed)
            elevation, metadata = source.get_elevation_data(self.bounds, 
                                                            self.settings.mesh_resolution)
            
            self.progress.emit(30, "Processing elevation data...")
            
            processor = TerrainProcessor(self.settings)
            processor.load_elevation_data(elevation, self.bounds)
            
            self.progress.emit(50, "Generating heightfield...")
            
            X, Y, Z = processor.generate_heightfield()
            
            self.progress.emit(70, "Building mesh...")
            
            generator = MeshGenerator()
            mesh = generator.generate_terrain_mesh(X, Y, Z, base_z=0)
            
            self.progress.emit(90, "Validating mesh...")
            
            is_valid, issues = mesh.validate()
            if not is_valid:
                print(f"Mesh validation issues: {issues}")
                
            self.progress.emit(100, "Complete!")
            self.finished.emit(mesh)
            
        except Exception as e:
            self.error.emit(str(e))


class MainWindow(QMainWindow):
    """Main application window."""
    
    def __init__(self):
        super().__init__()
        self.setWindowTitle("3D Terrain Map Generator - Optimized for Bambu Labs")
        self.setMinimumSize(1200, 800)
        
        self.current_mesh = None
        self.worker = None
        
        self.setup_ui()
        self.setup_menus()
        self.setup_toolbar()
        self.setup_statusbar()
        
    def setup_ui(self):
        """Setup the main UI layout."""
        central_widget = QWidget()
        self.setCentralWidget(central_widget)
        main_layout = QHBoxLayout(central_widget)
        
        # Create main splitter
        splitter = QSplitter(Qt.Orientation.Horizontal)
        
        # Left panel - Map selection
        left_panel = QWidget()
        left_layout = QVBoxLayout(left_panel)
        left_layout.setContentsMargins(0, 0, 0, 0)
        
        left_label = QLabel("Location Selection")
        left_label.setStyleSheet("font-weight: bold; padding: 5px;")
        left_layout.addWidget(left_label)
        
        self.map_panel = MapSelectionPanel()
        left_layout.addWidget(self.map_panel)
        
        splitter.addWidget(left_panel)
        
        # Center - 3D viewport
        center_panel = QWidget()
        center_layout = QVBoxLayout(center_panel)
        center_layout.setContentsMargins(0, 0, 0, 0)
        
        self.viewport = Viewport3D()
        center_layout.addWidget(self.viewport)
        
        # Progress bar
        self.progress_bar = QProgressBar()
        self.progress_bar.setVisible(False)
        center_layout.addWidget(self.progress_bar)
        
        # Action buttons
        btn_layout = QHBoxLayout()
        
        self.generate_btn = QPushButton("Generate Terrain")
        self.generate_btn.setStyleSheet("padding: 10px; font-weight: bold;")
        self.generate_btn.clicked.connect(self.generate_terrain)
        btn_layout.addWidget(self.generate_btn)
        
        self.export_btn = QPushButton("Export STL")
        self.export_btn.setStyleSheet("padding: 10px;")
        self.export_btn.clicked.connect(self.export_stl)
        self.export_btn.setEnabled(False)
        btn_layout.addWidget(self.export_btn)
        
        center_layout.addLayout(btn_layout)
        
        splitter.addWidget(center_panel)
        
        # Right panel - Settings
        right_panel = QTabWidget()
        
        self.terrain_settings = TerrainSettingsPanel()
        right_panel.addTab(self.terrain_settings, "Terrain")
        
        self.printer_settings = PrinterSettingsPanel()
        right_panel.addTab(self.printer_settings, "Printer")
        
        splitter.addWidget(right_panel)
        
        # Set splitter proportions
        splitter.setSizes([250, 600, 350])
        
        main_layout.addWidget(splitter)
        
    def setup_menus(self):
        """Setup menu bar."""
        menubar = self.menuBar()
        
        # File menu
        file_menu = menubar.addMenu("&File")
        
        new_action = QAction("&New Project", self)
        new_action.setShortcut("Ctrl+N")
        new_action.triggered.connect(self.new_project)
        file_menu.addAction(new_action)
        
        open_action = QAction("&Open Project", self)
        open_action.setShortcut("Ctrl+O")
        open_action.triggered.connect(self.open_project)
        file_menu.addAction(open_action)
        
        save_action = QAction("&Save Project", self)
        save_action.setShortcut("Ctrl+S")
        save_action.triggered.connect(self.save_project)
        file_menu.addAction(save_action)
        
        file_menu.addSeparator()
        
        import_dem_action = QAction("Import &DEM File...", self)
        import_dem_action.triggered.connect(self.import_dem)
        file_menu.addAction(import_dem_action)
        
        file_menu.addSeparator()
        
        export_stl_action = QAction("&Export STL...", self)
        export_stl_action.setShortcut("Ctrl+E")
        export_stl_action.triggered.connect(self.export_stl)
        file_menu.addAction(export_stl_action)
        
        export_3mf_action = QAction("Export &3MF...", self)
        export_3mf_action.triggered.connect(self.export_3mf)
        file_menu.addAction(export_3mf_action)
        
        file_menu.addSeparator()
        
        exit_action = QAction("E&xit", self)
        exit_action.setShortcut("Alt+F4")
        exit_action.triggered.connect(self.close)
        file_menu.addAction(exit_action)
        
        # Edit menu
        edit_menu = menubar.addMenu("&Edit")
        
        undo_action = QAction("&Undo", self)
        undo_action.setShortcut("Ctrl+Z")
        edit_menu.addAction(undo_action)
        
        redo_action = QAction("&Redo", self)
        redo_action.setShortcut("Ctrl+Y")
        edit_menu.addAction(redo_action)
        
        # View menu
        view_menu = menubar.addMenu("&View")
        
        reset_view_action = QAction("&Reset View", self)
        reset_view_action.setShortcut("Home")
        view_menu.addAction(reset_view_action)
        
        # Help menu
        help_menu = menubar.addMenu("&Help")
        
        about_action = QAction("&About", self)
        about_action.triggered.connect(self.show_about)
        help_menu.addAction(about_action)
        
    def setup_toolbar(self):
        """Setup toolbar."""
        toolbar = QToolBar()
        toolbar.setMovable(False)
        self.addToolBar(toolbar)
        
        # New action
        new_action = toolbar.addAction("New")
        new_action.triggered.connect(self.new_project)
        
        # Open action
        open_action = toolbar.addAction("Open")
        open_action.triggered.connect(self.open_project)
        
        # Save action
        save_action = toolbar.addAction("Save")
        save_action.triggered.connect(self.save_project)
        
        toolbar.addSeparator()
        
        # Generate action
        generate_action = toolbar.addAction("Generate")
        generate_action.triggered.connect(self.generate_terrain)
        
        # Export action
        export_action = toolbar.addAction("Export")
        export_action.triggered.connect(self.export_stl)
        
    def setup_statusbar(self):
        """Setup status bar."""
        self.statusbar = QStatusBar()
        self.setStatusBar(self.statusbar)
        self.statusbar.showMessage("Ready - Select location and generate terrain")
        
    def generate_terrain(self):
        """Generate terrain mesh from current settings."""
        if self.worker and self.worker.isRunning():
            return
            
        bounds = self.map_panel.get_bounds()
        settings = self.terrain_settings.get_settings()
        
        # Check printer compatibility
        printer = self.printer_settings.get_current_printer()
        is_valid, msg = printer.validate_dimensions(
            settings.map_width, settings.map_height, settings.max_print_height
        )
        if not is_valid:
            # Make the non-blocking validation explicit: ask the user whether to continue.
            detailed_msg = (
                msg
                + "\n\nThe generated mesh may not fit on the selected printer.\n"
                + "Do you want to continue generation anyway?"
            )
            reply = QMessageBox.warning(
                self,
                "Dimension Warning",
                detailed_msg,
                QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
                QMessageBox.StandardButton.No,
            )
            if reply != QMessageBox.StandardButton.Yes:
                # User chose not to proceed; cancel generation.
                self.statusbar.showMessage(
                    "Generation cancelled due to printer dimension limits"
                )
                return
            
        self.progress_bar.setVisible(True)
        self.progress_bar.setValue(0)
        self.generate_btn.setEnabled(False)
        self.statusbar.showMessage("Generating terrain mesh...")
        
        self.worker = GenerateMeshWorker(bounds, settings)
        self.worker.progress.connect(self._on_progress)
        self.worker.finished.connect(self._on_generation_complete)
        self.worker.error.connect(self._on_generation_error)
        self.worker.start()
        
    def _on_progress(self, value: int, message: str):
        self.progress_bar.setValue(value)
        self.statusbar.showMessage(message)
        
    def _on_generation_complete(self, mesh: MeshData):
        self.current_mesh = mesh
        self.viewport.set_mesh(mesh)
        self.progress_bar.setVisible(False)
        self.generate_btn.setEnabled(True)
        self.export_btn.setEnabled(True)
        self.statusbar.showMessage(
            f"Terrain generated: {mesh.vertex_count:,} vertices, {mesh.face_count:,} faces"
        )
        
    def _on_generation_error(self, error: str):
        self.progress_bar.setVisible(False)
        self.generate_btn.setEnabled(True)
        self.statusbar.showMessage(f"Error: {error}")
        QMessageBox.critical(self, "Generation Error", f"Failed to generate terrain:\n{error}")
        
    def export_stl(self):
        """Export current mesh to STL file."""
        if self.current_mesh is None:
            QMessageBox.warning(self, "No Mesh", "Please generate terrain first.")
            return
            
        filepath, _ = QFileDialog.getSaveFileName(
            self, "Export STL", "", "STL Files (*.stl);;All Files (*)"
        )
        
        if filepath:
            if not filepath.lower().endswith('.stl'):
                filepath += '.stl'
                
            try:
                exporter = STLExporter()
                
                # Validate before export
                validation = exporter.validate_for_printing(self.current_mesh)
                
                format_idx = self.printer_settings.format_combo.currentIndex()
                binary = format_idx == 0  # Binary is first option
                
                exporter.export(self.current_mesh, filepath, binary=binary)
                
                # Show statistics
                stats = validation["statistics"]
                msg = f"""STL exported successfully!

File: {filepath}
Dimensions: {stats['dimensions_mm']['width']:.1f} × {stats['dimensions_mm']['depth']:.1f} × {stats['dimensions_mm']['height']:.1f} mm
Triangles: {stats['face_count']:,}
File size: {stats['estimated_file_size_mb']:.2f} MB"""
                
                QMessageBox.information(self, "Export Complete", msg)
                self.statusbar.showMessage(f"Exported to {filepath}")
                
            except Exception as e:
                QMessageBox.critical(self, "Export Error", f"Failed to export:\n{e}")
                
    def export_3mf(self):
        """Export current mesh to 3MF file."""
        if self.current_mesh is None:
            QMessageBox.warning(self, "No Mesh", "Please generate terrain first.")
            return
            
        filepath, _ = QFileDialog.getSaveFileName(
            self, "Export 3MF", "", "3MF Files (*.3mf);;All Files (*)"
        )
        
        if filepath:
            if not filepath.lower().endswith('.3mf'):
                filepath += '.3mf'
                
            try:
                exporter = ThreeMFExporter()
                exporter.export(self.current_mesh, filepath, metadata={
                    "title": "Terrain Map",
                    "application": "3D Terrain Map Generator"
                })
                
                QMessageBox.information(self, "Export Complete", 
                                       f"3MF exported successfully!\n\nFile: {filepath}")
                self.statusbar.showMessage(f"Exported to {filepath}")
                
            except Exception as e:
                QMessageBox.critical(self, "Export Error", f"Failed to export:\n{e}")
                
    def import_dem(self):
        """Import DEM file."""
        filepath, _ = QFileDialog.getOpenFileName(
            self, "Import DEM File", "",
            "DEM Files (*.tif *.tiff *.hgt *.asc);;All Files (*)"
        )
        
        if filepath:
            self.statusbar.showMessage(f"Imported: {filepath}")
            QMessageBox.information(self, "Import", 
                                   f"DEM file selected:\n{filepath}\n\n"
                                   "Note: Full DEM import requires rasterio library.")
    
    def new_project(self):
        """Create a new project, resetting all settings."""
        reply = QMessageBox.question(
            self, "New Project",
            "Create a new project? Any unsaved changes will be lost.",
            QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
            QMessageBox.StandardButton.No
        )
        
        if reply == QMessageBox.StandardButton.Yes:
            # Reset to default location (Matterhorn)
            self.map_panel.set_location(45.9766, 7.6586)
            
            # Reset terrain settings
            self.terrain_settings.exaggeration_spin.setValue(2.0)
            self.terrain_settings.smoothing_spin.setValue(1.0)
            self.terrain_settings.width_spin.setValue(150)
            self.terrain_settings.height_spin.setValue(150)
            self.terrain_settings.max_height_spin.setValue(30)
            self.terrain_settings.base_type_combo.setCurrentIndex(0)
            self.terrain_settings.base_thickness_spin.setValue(3)
            self.terrain_settings.resolution_spin.setValue(256)
            
            # Clear current mesh
            self.current_mesh = None
            self.viewport.mesh_data = None
            self.viewport.info_label.setText("3D Preview\n\nLoad terrain data to see preview")
            self.viewport.info_label.setStyleSheet("color: #888; font-size: 14px;")
            self.export_btn.setEnabled(False)
            
            self.statusbar.showMessage("New project created")
    
    def open_project(self):
        """Open a project file."""
        filepath, _ = QFileDialog.getOpenFileName(
            self, "Open Project", "",
            "Terrain Project Files (*.tproj *.json);;All Files (*)"
        )
        
        if filepath:
            try:
                import json
                with open(filepath, 'r') as f:
                    project = json.load(f)
                
                # Load location
                if 'location' in project:
                    self.map_panel.lat_spin.setValue(project['location'].get('lat', 45.9766))
                    self.map_panel.lon_spin.setValue(project['location'].get('lon', 7.6586))
                    self.map_panel.area_width.setValue(project['location'].get('width', 10))
                    self.map_panel.area_height.setValue(project['location'].get('height', 10))
                
                # Load terrain settings
                if 'terrain' in project:
                    t = project['terrain']
                    self.terrain_settings.exaggeration_spin.setValue(t.get('exaggeration', 2.0))
                    self.terrain_settings.smoothing_spin.setValue(t.get('smoothing', 1.0))
                    self.terrain_settings.width_spin.setValue(t.get('map_width', 150))
                    self.terrain_settings.height_spin.setValue(t.get('map_height', 150))
                    self.terrain_settings.max_height_spin.setValue(t.get('max_height', 30))
                    self.terrain_settings.base_thickness_spin.setValue(t.get('base_thickness', 3))
                    self.terrain_settings.resolution_spin.setValue(t.get('resolution', 256))
                    
                    # Set base type
                    base_type = t.get('base_type', 'flat')
                    idx = self.terrain_settings.base_type_combo.findText(base_type)
                    if idx >= 0:
                        self.terrain_settings.base_type_combo.setCurrentIndex(idx)
                
                self.statusbar.showMessage(f"Project loaded: {filepath}")
                
            except Exception as e:
                QMessageBox.critical(self, "Open Error", f"Failed to open project:\n{e}")
    
    def save_project(self):
        """Save the current project to a file."""
        filepath, _ = QFileDialog.getSaveFileName(
            self, "Save Project", "",
            "Terrain Project Files (*.tproj);;JSON Files (*.json);;All Files (*)"
        )
        
        if filepath:
            if not filepath.lower().endswith(('.tproj', '.json')):
                filepath += '.tproj'
            
            try:
                import json
                project = {
                    'version': '1.0',
                    'location': {
                        'lat': self.map_panel.lat_spin.value(),
                        'lon': self.map_panel.lon_spin.value(),
                        'width': self.map_panel.area_width.value(),
                        'height': self.map_panel.area_height.value(),
                    },
                    'terrain': {
                        'exaggeration': self.terrain_settings.exaggeration_spin.value(),
                        'smoothing': self.terrain_settings.smoothing_spin.value(),
                        'map_width': self.terrain_settings.width_spin.value(),
                        'map_height': self.terrain_settings.height_spin.value(),
                        'max_height': self.terrain_settings.max_height_spin.value(),
                        'base_type': self.terrain_settings.base_type_combo.currentText(),
                        'base_thickness': self.terrain_settings.base_thickness_spin.value(),
                        'resolution': self.terrain_settings.resolution_spin.value(),
                    }
                }
                
                with open(filepath, 'w') as f:
                    json.dump(project, f, indent=2)
                
                self.statusbar.showMessage(f"Project saved: {filepath}")
                
            except Exception as e:
                QMessageBox.critical(self, "Save Error", f"Failed to save project:\n{e}")
            
    def show_about(self):
        """Show about dialog."""
        QMessageBox.about(self, "About 3D Terrain Map Generator",
            """<h2>3D Terrain Map Generator</h2>
            <p>Version 1.0.0</p>
            <p>A Windows application for creating 3D-printable terrain maps 
            from geographic elevation data.</p>
            <p><b>Optimized for Bambu Labs Printers:</b><br>
            • X1 Carbon, X1E<br>
            • P1P, P1S, P2S<br>
            • A1, A1 mini</p>
            <p><b>Features:</b><br>
            • Terrain mesh generation<br>
            • STL and 3MF export<br>
            • Customizable dimensions<br>
            • Vertical exaggeration<br>
            • Multiple base types</p>
            """)


def main():
    """Main entry point."""
    app = QApplication(sys.argv)
    
    # Set application style
    app.setStyle("Fusion")
    
    # Create and show main window
    window = MainWindow()
    window.show()
    
    sys.exit(app.exec())


if __name__ == "__main__":
    main()

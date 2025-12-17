# Assets Directory

This directory should contain the application logo and other assets.

## Required Assets

1. **Logo.png** (20x20 pixels minimum)
   - Used in the title bar of MainWindow.xaml
   - Should be a transparent PNG
   - Recommended size: 20x20 to 32x32 pixels

## Adding Assets

To add the logo:
1. Add your logo file as `Logo.png` to this directory
2. Right-click the file in Visual Studio
3. Set Build Action to "Resource"
4. The file will be embedded in the application

## Note

The application will continue to work without the logo, but you may see
a warning in the XAML designer. The image control will simply not display
anything if the file is missing.

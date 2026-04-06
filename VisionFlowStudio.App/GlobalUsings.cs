// Global using directives to resolve type conflicts between WPF and WinForms
// When UseWindowsForms is enabled, these types become ambiguous

global using Brush = System.Windows.Media.Brush;
global using Brushes = System.Windows.Media.Brushes;
global using Color = System.Windows.Media.Color;
global using ColorConverter = System.Windows.Media.ColorConverter;
global using Point = System.Windows.Point;
global using Size = System.Windows.Size;
global using MouseEventArgs = System.Windows.Input.MouseEventArgs;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
global using Cursors = System.Windows.Input.Cursors;
global using Image = System.Windows.Controls.Image;
global using Application = System.Windows.Application;
global using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
global using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

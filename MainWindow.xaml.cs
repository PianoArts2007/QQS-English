using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using QQS_UI.Core;
using Path = System.IO.Path;
using System.Diagnostics;
using System.Threading;

namespace QQS_UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private RenderFile file = null;
        private bool isLoading = false;
        private Core.RenderOptions options = Core.RenderOptions.CreateRenderOptions();
        private CommonRenderer renderer = null;
        private readonly Config config;
        private readonly CustomColor customColors;
        private int keyHeightPercentage = 15;
        private const string DefaultVideoFilter = "Video (*.mp4, *.avi, *.mov)|*.mp4;*.avi;*.mov",
            PNGVideoFilter = "Video (*.mp4, *.mov)|*.mp4, *.mov",
            TransparentVideoFilter = "Video (*.mov)|*.mov";
        public MainWindow()
        {
            InitializeComponent();
            config = new Config();
            customColors = new CustomColor();
            if (config.CachedMIDIDirectory == null)
            {
                config.CachedMIDIDirectory = new OpenFileDialog().InitialDirectory;
            }
            if (config.CachedVideoDirectory == null)
            {
                config.CachedVideoDirectory = new SaveFileDialog().InitialDirectory;
            }
            if (config.CachedColorDirectory == null)
            {
                config.CachedColorDirectory = config.CachedVideoDirectory;
            }
            config.SaveConfig();
            previewColor.Background = new SolidColorBrush(new Color
            {
                R = (byte)(options.DivideBarColor & 0xff),
                G = (byte)((options.DivideBarColor & 0xff00) >> 8),
                B = (byte)((options.DivideBarColor & 0xff0000) >> 16),
                A = 0xff
            });
            previewBackgroundColor.Background = new SolidColorBrush(new Color
            {
                R = 0,
                G = 0,
                B = 0,
                A = 255
            });

            if (!PFAConfigrationLoader.IsConfigurationAvailable)
            {
                loadPFAColors.IsEnabled = false;
            }

            renderWidth.Value = 1920;
            renderHeight.Value = 1080;
            noteSpeed.Value = 1.5;
#if DEBUG
            Title += " (Debug)";
#endif
            unpressedKeyboardGradientStrength.Value = Global.DefaultUnpressedWhiteKeyGradientScale;
            pressedKeyboardGradientStrength.Value = Global.DefaultPressedWhiteKeyGradientScale;
            noteGradientStrength.Value = Global.DefaultNoteGradientScale;
            separatorGradientStrength.Value = Global.DefaultSeparatorGradientScale;
        }

        private void openMidi_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "MIDI File (*.mid)|*.mid",
                InitialDirectory = config.CachedMIDIDirectory
            };
            if ((bool)dialog.ShowDialog())
            {
                string midiDirectory = Path.GetDirectoryName(Path.GetFullPath(dialog.FileName));
                config.CachedMIDIDirectory = midiDirectory;
                midiPath.Text = dialog.FileName;
                config.SaveConfig();
            }
        }

        private void loadButton_Click(object sender, RoutedEventArgs e)
        {
            if (isLoading)
            {
                return;
            }
            string fileName = midiPath.Text;
            if (!File.Exists(fileName) || !fileName.EndsWith(".mid"))
            {
                _ = MessageBox.Show("Incorrect MIDI path.", "Cannot load MIDI file.");
                return;
            }
            trackCount.Content = "Loading...";
            noteCount.Content = "Loading...";
            _ = Task.Run(() =>
            {
                isLoading = true;
                file = new RenderFile(fileName);
                isLoading = false;
                TimeSpan midilen = Global.GetTimeOf(file.MidiTime, file.Division, file.Tempos);
                Dispatcher.Invoke(() =>
                {
                    Resources["midiLoaded"] = true;
                    trackCount.Content = file.TrackCount.ToString();
                    noteCount.Content = file.NoteCount.ToString();
                    midiLen.Content = midilen.ToString("mm\\:ss\\.fff");
                });
            });
        }

        private void unloadButton_Click(object sender, RoutedEventArgs e)
        {
            int gen = GC.GetGeneration(file);
            file = null;
            GC.Collect(gen);
            Resources["midiLoaded"] = false;
            Console.WriteLine("Unloaded MIDI.");
            noteCount.Content = "-";
            trackCount.Content = "-";
            midiLen.Content = "--:--.---";
        }

        private void noteSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            options.NoteSpeed = noteSpeed.Value;
        }

        private void selectOutput_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog()
            {
                Filter = options.TransparentBackground ? TransparentVideoFilter : (options.PNGEncoder ? PNGVideoFilter : DefaultVideoFilter),
                Title = "Video output path",
                InitialDirectory = config.CachedVideoDirectory
            };
            if ((bool)dialog.ShowDialog())
            {
                config.CachedVideoDirectory = Path.GetDirectoryName(Path.GetFullPath(dialog.FileName));
                outputPath.Text = dialog.FileName;
                config.SaveConfig();
            }
        }

        private void startRender_Click(object sender, RoutedEventArgs e)
        {
            if (file == null)
            {
                _ = MessageBox.Show("Unable to render: \nNo MIDI file selected.", "No MIDI file");
                return;
            }
            options.Input = midiPath.Text;
            options.Output = outputPath.Text;
            options.PreviewMode = false;
            options.AdditionalFFMpegArgument = additionalFFArgs.Text;
            Resources["notRendering"] = Resources["notRenderingOrPreviewing"] = false;
            renderer = new CommonRenderer(file, options);
            _ = Task.Run(() =>
            {
                Console.WriteLine("准备渲染...");
                renderer.Render();
                int gen = GC.GetGeneration(renderer);
                Dispatcher.Invoke(() =>
                {
                    renderer = null;
                    Resources["notRendering"] = Resources["notRenderingOrPreviewing"] = true;
                });
                GC.Collect(gen);
            });
        }

        private void interruptButton_Click(object sender, RoutedEventArgs e)
        {
            if (renderer != null)
            {
                renderer.Interrupt = true;
            }
        }

        private void crfSelect_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            options.CRF = (int)crfSelect.Value;
        }

        private void enableTranparentBackground_CheckToggled(object sender, RoutedPropertyChangedEventArgs<bool> e)
        {
            options.TransparentBackground = enableTranparentBackground.IsChecked;
            if (options.TransparentBackground)
            {
                if (!outputPath.Text.EndsWith(".mov"))
                {
                    outputPath.Text = outputPath.Text.Substring(0, outputPath.Text.Length - 4) + ".mov";
                }
            }
        }
        private void startPreview_Click(object sender, RoutedEventArgs e)
        {
            if (file == null)
            {
                _ = MessageBox.Show("Unable to preview: \nNo MIDI file selected.", "No MIDI file");
                return;
            }
            if (usePNGEncoder.IsChecked)
            {
                _ = MessageBox.Show("Unable to preview: \nCannot preview when using PNG sequence.", "Unable to preview");
                return;
            }
            options.Input = midiPath.Text;
            options.Output = outputPath.Text;
            options.PreviewMode = true;
            options.AdditionalFFMpegArgument = additionalFFArgs.Text;
            Resources["notPreviewing"] = Resources["notRenderingOrPreviewing"] = false;
            renderer = new CommonRenderer(file, options);
            _ = Task.Run(() =>
            {
                Console.WriteLine("Ready to preview...");
                renderer.Render();
                int gen = GC.GetGeneration(renderer);
                Dispatcher.Invoke(() =>
                {
                    renderer = null;
                    Resources["notPreviewing"] = Resources["notRenderingOrPreviewing"] = true;
                });
                GC.Collect(gen);
            });
        }

        private void useDefaultColors_Click(object sender, RoutedEventArgs e)
        {
            customColors.UseDefault();
            customColors.SetGlobal();
            _ = MessageBox.Show("Colours reset.", "Colours reset");
        }

        private void loadColors_Click(object sender, RoutedEventArgs e)
        {
            string filePath = colorPath.Text;
            if (!filePath.EndsWith(".json"))
            {
                _ = MessageBox.Show("Unable to load palette.\nPalettes are only supported in JSON format.", "Unable to load palette");
                return;
            }
            if (!File.Exists(filePath))
            {
                _ = MessageBox.Show("Unable to load palette: The palette does not exist.", "Unable to load palette");
                return;
            }
            int errCode = customColors.Load(filePath);
            if (errCode == 1)
            {
                _ = MessageBox.Show("Unable to load palette: Incorrect file format.", "Unable to load palette");
                return;
            }
            errCode = customColors.SetGlobal();
            if (errCode != 0)
            {
                _ = MessageBox.Show("Unable to load palette: Palette is empty.", "Unable to load palette");
                return;
            }
            _ = MessageBox.Show("Palette loaded. Loaded " + customColors.Colors.Length + " colours.", "Loaded palette");
        }

        private void openColorFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "JSON File (*.json)|*.json",
                InitialDirectory = config.CachedColorDirectory
            };
            if ((bool)dialog.ShowDialog())
            {
                string colorDirectory = Path.GetDirectoryName(Path.GetFullPath(dialog.FileName));
                config.CachedColorDirectory = colorDirectory;
                colorPath.Text = dialog.FileName;
                config.SaveConfig();
            }
        }

        private void limitPreviewFPS_CheckToggled(object sender, RoutedPropertyChangedEventArgs<bool> e)
        {
            Global.LimitPreviewFPS = e.NewValue;
        }

        private void loadPFAColors_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RGBAColor[] colors = PFAConfigrationLoader.LoadPFAConfigurationColors();
                customColors.Colors = colors;
                _ = customColors.SetGlobal();
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show($"Unable to load PFA palette: \n{ex.Message}\nStack trace: \n{ex.StackTrace}", "Unable to load PFA palette");
            }
        }

        private void setbgColor_Click(object sender, RoutedEventArgs e)
        {
            string coltxt = bgColor.Text;
            if (coltxt.Length != 6)
            {
                _ = MessageBox.Show("Invalid colour code.\nThe colour has to be in 6-digit hexadecimal format.", "Invalid colour code");
                return;
            }
            try
            {
                byte r = Convert.ToByte(coltxt.Substring(0, 2), 16);
                byte g = Convert.ToByte(coltxt.Substring(2, 2), 16);
                byte b = Convert.ToByte(coltxt.Substring(4, 2), 16);
                uint col = 0xff000000U | r | (uint)(g << 8) | (uint)(b << 16);
                options.BackgroundColor = col;
                previewBackgroundColor.Background = new SolidColorBrush(new Color()
                {
                    R = r,
                    G = g,
                    B = b,
                    A = 0xff
                });
            }
            catch
            {
                _ = MessageBox.Show("Invalid colour code.\nThe colour code is incorrect.", "Invalid colour code");
            }
        }

        private void drawGreySquare_CheckToggled(object sender, RoutedPropertyChangedEventArgs<bool> e)
        {
            options.DrawGreySquare = e.NewValue;
        }

        private void enableNoteColorGradient_CheckToggled(object sender, RoutedPropertyChangedEventArgs<bool> e)
        {
            options.Gradient = e.NewValue;
        }

        private void shuffleColor_Click(object sender, RoutedEventArgs e)
        {
            customColors.Shuffle().SetGlobal();
        }

        private void enableSeparator_CheckToggled(object sender, RoutedPropertyChangedEventArgs<bool> e)
        {
            options.DrawSeparator = e.NewValue;
        }

        private void thinnerNotes_CheckToggled(object sender, RoutedPropertyChangedEventArgs<bool> e)
        {
            options.ThinnerNotes = e.NewValue;
        }

        private void fps_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            options.FPS = (int)e.NewValue;
        }

        private void renderWidth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            options.Width = (int)e.NewValue;
        }

        private void renderHeight_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            options.Height = (int)e.NewValue;
            options.KeyHeight = options.Height * keyHeightPercentage / 100;
        }

        private void presetResolution_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (presetResolution.SelectedIndex)
            {
                case 0:
                    renderWidth.Value = 640;
                    renderHeight.Value = 480;
                    break;
                case 1:
                    renderWidth.Value = 1280;
                    renderHeight.Value = 720;
                    break;
                case 2:
                    renderWidth.Value = 1920;
                    renderHeight.Value = 1080;
                    break;
                case 3:
                    renderWidth.Value = 2560;
                    renderHeight.Value = 1440;
                    break;
                default:
                    renderWidth.Value = 3840;
                    renderHeight.Value = 2160;
                    break;
            }
        }

        private void keyboardHeightPercentage_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            keyHeightPercentage = (int)e.NewValue;
            options.KeyHeight = options.Height * keyHeightPercentage / 100;
        }

        private void delayStart_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            options.DelayStartSeconds = (double)e.NewValue;
        }

        private void resetGradientScale_Click(object sender, RoutedEventArgs e)
        {
            unpressedKeyboardGradientStrength.Value = Global.DefaultUnpressedWhiteKeyGradientScale;
            pressedKeyboardGradientStrength.Value = Global.DefaultPressedWhiteKeyGradientScale;
            noteGradientStrength.Value = Global.DefaultNoteGradientScale;
            separatorGradientStrength.Value = Global.DefaultSeparatorGradientScale;

            unpressedKeyboardGradientStrength.slider.Value = Global.DefaultUnpressedWhiteKeyGradientScale;
            pressedKeyboardGradientStrength.slider.Value = Global.DefaultPressedWhiteKeyGradientScale;
            noteGradientStrength.slider.Value = Global.DefaultNoteGradientScale;
            separatorGradientStrength.slider.Value = Global.DefaultSeparatorGradientScale;

            options.KeyboardGradientDirection = VerticalGradientDirection.FromButtomToTop;
            options.SeparatorGradientDirection = VerticalGradientDirection.FromButtomToTop;
            options.NoteGradientDirection = HorizontalGradientDirection.FromLeftToRight;

            keyboardGradientDirection.SelectedIndex = 0;
            noteGradientDirection.SelectedIndex = 0;
            barGradientDirection.SelectedIndex = 0;
        }

        private void noteGradientStrength_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Global.NoteGradientScale = e.NewValue;
        }

        private void unpressedKeyboardGradientStrength_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Global.UnpressedWhiteKeyGradientScale = e.NewValue;
        }

        private void pressedKeyboardGradientStrength_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Global.PressedWhiteKeyGradientScale = e.NewValue;
        }

        private void separatorGradientStrength_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Global.SeparatorGradientScale = e.NewValue;
        }

        private void noteGradientDirection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            options.NoteGradientDirection = (HorizontalGradientDirection)noteGradientDirection.SelectedIndex;
        }

        private void keyboardGradientDirection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            options.KeyboardGradientDirection = (VerticalGradientDirection)keyboardGradientDirection.SelectedIndex;
        }

        private void barGradientDirection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            options.SeparatorGradientDirection = (VerticalGradientDirection)barGradientDirection.SelectedIndex;
        }

        private void setBarColor_Click(object sender, RoutedEventArgs e)
        {
            string coltxt = barColor.Text;
            if (coltxt.Length != 6)
            {
                _ = MessageBox.Show("Invalid colour code.\nThe colour has to be in 6-digit hexadecimal format.", "Invalid colour code");
                return;
            }
            try
            {
                byte r = Convert.ToByte(coltxt.Substring(0, 2), 16);
                byte g = Convert.ToByte(coltxt.Substring(2, 2), 16);
                byte b = Convert.ToByte(coltxt.Substring(4, 2), 16);
                uint col = 0xff000000U | r | (uint)(g << 8) | (uint)(b << 16);
                options.DivideBarColor = col;
                previewColor.Background = new SolidColorBrush(new Color()
                {
                    R = r,
                    G = g,
                    B = b,
                    A = 0xff
                });
            }
            catch
            {
                _ = MessageBox.Show("Invalid colour code.\nThe colour code is incorrect.", "Invalid colour code");
            }
        }

        private void usePNGEncoder_CheckToggled(object sender, RoutedPropertyChangedEventArgs<bool> e)
        {
            if (outputPath.Text != null)
            {
                if (outputPath.Text.EndsWith(".avi") && e.NewValue)
                {
                    _ = MessageBox.Show("Note: AVI video isn't currently supported with PNG sequence.\nPlease save as MP4 or MOV.", "Cannot be set as PNG encoder");
                    e.Handled = true;
                    usePNGEncoder.IsChecked = false;
                    return;
                }
            }
            options.PNGEncoder = usePNGEncoder.IsChecked;
            if (!options.PNGEncoder)
            {
                enableTranparentBackground.IsChecked = false;
                options.TransparentBackground = false;
            }
        }
    }

    internal class NotValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    internal class AndValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = true;
            foreach (object obj in values)
            {
                b &= (bool)obj;
            }
            return b;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

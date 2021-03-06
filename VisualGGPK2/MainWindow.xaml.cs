﻿using LibGGPK2;
using LibGGPK2.Records;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VisualGGPK2
{
    public partial class MainWindow : Window
    {
        public GGPKContainer ggpkContainer;
        /// <summary>
        /// Icon of directory on TreeView
        /// </summary>
        public static readonly BitmapFrame IconDir = BitmapFrame.Create(new MemoryStream((byte[])Properties.Resources.ResourceManager.GetObject("dir")));
        /// <summary>
        /// Icon of file on TreeView
        /// </summary>
        public static readonly BitmapFrame IconFile = BitmapFrame.Create(new MemoryStream((byte[])Properties.Resources.ResourceManager.GetObject("file")));
        public static readonly ContextMenu TreeMenu = new ContextMenu();
        public static readonly Encoding Unicode = new UnicodeEncoding(false, true);
        public static readonly Encoding UTF8 = new UTF8Encoding(false, false);

        public MainWindow() { InitializeComponent(); }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                DefaultExt = "ggpk",
                FileName = "Content.ggpk",
                Filter = "GGPK File|*.ggpk"
            };

            var setting = Properties.Settings.Default;
            if (setting.GGPKPath == "")
            {
                var path = Registry.CurrentUser.OpenSubKey(@"Software\GrindingGearGames\Path of Exile")?.GetValue("InstallLocation") as string;
                if (path != null && File.Exists(path + @"\Content.ggpk"))
                    ofd.InitialDirectory = path.TrimEnd('\\'); // Get POE path
                else
                {
                    path = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Garena\PoE")?.GetValue("Path") as string;
                    if (path != null && File.Exists(path + @"\Content.ggpk"))
                        ofd.InitialDirectory = path.TrimEnd('\\'); // Get Garena POE path
                }
            }
            else
                ofd.InitialDirectory = setting.GGPKPath;


            if (ofd.ShowDialog() == true) // Select Content.ggpk
            {
                setting.GGPKPath = Directory.GetParent(ofd.FileName).FullName;
                setting.Save();

                var mi = new MenuItem { Header = "Export" }; // Initial ContextMenu
                mi.Click += OnExportClicked;
                TreeMenu.Items.Add(mi);
                mi = new MenuItem { Header = "Replace" };
                mi.Click += OnReplaceClicked;
                TreeMenu.Items.Add(mi);

                var args = Environment.GetCommandLineArgs();
                ggpkContainer =  new GGPKContainer(ofd.FileName, args.Length > 1 && args[1] == "-bundleMode"); // Initial GGPK
                var root = CreateNode(ggpkContainer.rootDirectory);
                Tree.Items.Add(root); // Initial TreeView
                root.IsExpanded = true;

                var imageMenu = new ContextMenu();
                mi = new MenuItem { Header = "SaveAsPng" };
                mi.Click += OnSavePngClicked;
                imageMenu.Items.Add(mi);
                ImageView.ContextMenu = imageMenu;

                TextView.AppendText("\r\n\r\nDone!\r\n");
            }
            else
                Close();
        }

        /// <summary>
        /// Create a element of the TreeView
        /// </summary>
        public TreeViewItem CreateNode(RecordTreeNode rtn)
        {
            var tvi = new TreeViewItem { Tag = rtn };
            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(new Image // Icon
            {
                Source = rtn is IFileRecord ? IconFile : IconDir,
                Width = 20,
                Height = 20,
                Margin = new Thickness(0,0,2,0)
            });
            stack.Children.Add(new TextBlock { Text = rtn.Name, FontSize = 16 }); // File/Directory Name
            tvi.Header = stack;
            if (!(rtn is IFileRecord))
                tvi.Items.Add("Loading . . ."); // Add expand button
            tvi.ContextMenu = TreeMenu;
            return tvi;
        }

        /// <summary>
        /// Directory expanded event
        /// </summary>
        private void OnTreeExpanded(object sender, RoutedEventArgs e)
        {
            var tvi = e.Source as TreeViewItem;
            if (tvi.Items.Count == 1 && tvi.Items[0] is string) // Haven't been expanded yet
            {
                tvi.Items.Clear();
                foreach (var c in ((RecordTreeNode)tvi.Tag).Children)
                    tvi.Items.Add(CreateNode(c));
            }
        }

        /// <summary>
        /// TreeView selected changed event
        /// </summary>
        private void OnTreeSelectedChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem tvi)
            {
                ImageView.Visibility = Visibility.Hidden;
                TextView.Visibility = Visibility.Hidden;
                //OGGView.Visibility = Visibility.Hidden;
                //BK2View.Visibility = Visibility.Hidden;
                //BANKView.Visibility = Visibility.Hidden;
                ButtonSave.Visibility = Visibility.Hidden;
                if (tvi.Tag is RecordTreeNode rtn)
                {
                    TextBoxOffset.Text = rtn.Offset.ToString("X");
                    TextBoxSize.Text = rtn.Length.ToString();
                    TextBoxHash.Text = rtn is DirectoryRecord || rtn is FileRecord ? BitConverter.ToString(rtn.Hash).Replace("-", "") : rtn is BundleFileNode bf ? bf.Hash.ToString("X") : ((BundleDirectoryNode)rtn).Hash.ToString("X");
                    TextBoxBundle.Text = "";
                    if (rtn is IFileRecord f)
                    {
                        if (f is FileRecord fr) TextBoxSize.Text = fr.DataLength.ToString();
                        else TextBoxBundle.Text = ((BundleFileNode)f).BundleFileRecord.bundleRecord.Name;
                        switch (f is FileRecord fr2 ? fr2.DataFormat : ((BundleFileNode)rtn).DataFormat)
                        {
                            case IFileRecord.DataFormats.Image:
                                ImageView.Source = BitmapFrame.Create(new MemoryStream(f.ReadFileContent(ggpkContainer.fileStream)));
                                ImageView.Visibility = Visibility.Visible;
                                break;
                            case IFileRecord.DataFormats.Ascii:
                                TextView.IsReadOnly = false;
                                TextView.Tag = "UTF8";
                                TextView.Text = UTF8.GetString(f.ReadFileContent(ggpkContainer.fileStream));
                                TextView.Visibility = Visibility.Visible;
                                ButtonSave.Visibility = Visibility.Visible;
                                break;
                            case IFileRecord.DataFormats.Unicode:
                                if (rtn.Parent.Name == "Bundles")
                                    goto case IFileRecord.DataFormats.Ascii;
                                TextView.IsReadOnly = false;
                                TextView.Tag = "Unicode";
                                TextView.Text = Unicode.GetString(f.ReadFileContent(ggpkContainer.fileStream)).TrimStart('\xFEFF');
                                TextView.Visibility = Visibility.Visible;
                                ButtonSave.Visibility = Visibility.Visible;
                                break;
                            case IFileRecord.DataFormats.OGG:
                                //TODO
                                //OGGView.Visibility = Visibility.Visible;
                                break;
                            case IFileRecord.DataFormats.Dat:
                                //TODO
                                //DatView.Visibility = Visibility.Visible;
                                break;
                            case IFileRecord.DataFormats.TextureDds:
                                try
                                {
                                    var buffer = f.ReadFileContent(ggpkContainer.fileStream);
                                    while (buffer[0] == '*')
                                    {
                                        var path = UTF8.GetString(buffer, 1, buffer.Length - 1);
                                        f = (IFileRecord)ggpkContainer.FindRecord(path, ggpkContainer.FakeBundles2);
                                        buffer = f.ReadFileContent(ggpkContainer.fileStream);
                                    }
                                    if (buffer[0] != 'D' || buffer[1] != 'D' || buffer[2] != 'S' || buffer[3] != ' ')
                                        buffer = BrotliSharpLib.Brotli.DecompressBuffer(buffer, 4, buffer.Length - 4);
                                    var image = Pfim.Pfim.FromStream(new MemoryStream(buffer));
                                    image.Decompress();
                                    ImageView.Tag = rtn.Name;
                                    ImageView.Source = BitmapSource.Create(image.Width, image.Height, 96.0, 96.0,
                                    PixelFormat(image), null, image.Data, image.Stride);
                                    ImageView.Visibility = Visibility.Visible;
                                } catch (Exception ex) {
                                    TextView.Text = ex.ToString();
                                    TextView.IsReadOnly = true;
                                    TextView.Visibility = Visibility.Visible;
                                }
                                break;
                            case IFileRecord.DataFormats.BK2:
                                //TODO
                                //BK2View.Visibility = Visibility.Visible;
                                break;
                            case IFileRecord.DataFormats.BANK:
                                //TODO
                                //BANKView.Visibility = Visibility.Visible;
                                break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get the PixelFormat of the dds image
        /// </summary>
        public static PixelFormat PixelFormat(Pfim.IImage image)
        {
            switch (image.Format)
            {
                case Pfim.ImageFormat.Rgb24:
                    return PixelFormats.Bgr24;
                case Pfim.ImageFormat.Rgba32:
                    return PixelFormats.Bgr32;
                case Pfim.ImageFormat.Rgb8:
                    return PixelFormats.Gray8;
                case Pfim.ImageFormat.R5g5b5a1:
                case Pfim.ImageFormat.R5g5b5:
                    return PixelFormats.Bgr555;
                case Pfim.ImageFormat.R5g6b5:
                    return PixelFormats.Bgr565;
                default:
                    throw new Exception($"Unable to convert {image.Format} to WPF PixelFormat");
            }
        }

        /// <summary>
        /// TreeViewItem MouseDown event
        /// </summary>
        private void OnTreePreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!(e.Source is DependencyObject ui) || ui is TreeView) return;
            while (!(ui is TreeViewItem))
                ui = VisualTreeHelper.GetParent(ui);
            var tvi = ui as TreeViewItem;
            if (e.ChangedButton != MouseButton.Left) // Select when clicked
                tvi.IsSelected = true;
            else if ((tvi.Tag is DirectoryRecord || tvi.Tag is BundleDirectoryNode) && !(e.Source is TreeViewItem)) // Expand when left clicked
                tvi.IsExpanded = true;
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            if (!e.Effects.HasFlag(DragDropEffects.Copy)) return; // Drop File/Folder
            var dropped = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (dropped.Length != 1 || Path.GetFileName(dropped[0]) != "ROOT")
            {
                MessageBox.Show("The dropped directory must be \"ROOT\"", "Replace Faild",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (MessageBox.Show("Replace files?", "Replace Confirm",
                MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.OK)
            {
                var list = new Collection<KeyValuePair<IFileRecord, string>>();
                GGPKContainer.RecursiveFileList(ggpkContainer.rootDirectory, dropped[0], list, false);
                var bkg = new BackgroundDialog { ProgressText = "Replaced {0}/" + list.Count.ToString() + " Files . . ." };
                ggpkContainer.ReplaceAsync(list, bkg.NextProgress).ContinueWith((tsk) => {
                    if (tsk.Result == null)
                        MessageBox.Show("Replaced " + list.Count.ToString() + " Files", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        Dispatcher.Invoke(() => { throw tsk.Result; });
                    bkg.Close();
                });
                bkg.ShowDialog();
            }
        }

        private void OnExportClicked(object sender, RoutedEventArgs e)
        {
            if ((Tree.SelectedItem as TreeViewItem)?.Tag is RecordTreeNode rtn)
            {
                var sfd = new SaveFileDialog();
                if (rtn is IFileRecord fr)
                {
                    sfd.FileName = rtn.Name;
                    if (sfd.ShowDialog() == true)
                    {
                        File.WriteAllBytes(sfd.FileName, fr.ReadFileContent(ggpkContainer.fileStream));
                        MessageBox.Show("Exported " + rtn.GetPath(), "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    sfd.FileName = rtn.Name + ".dir";
                    if (sfd.ShowDialog() == true)
                    {
                        var list = new SortedDictionary<IFileRecord, string>(GGPKContainer.BundleComparer);
                        var path = Directory.GetParent(sfd.FileName).FullName + "\\" + rtn.Name;
                        GGPKContainer.RecursiveFileList(rtn, path, list, true);
                        var bkg = new BackgroundDialog { ProgressText = "Exported {0}/" + list.Count.ToString() + " Files . . ." };
                        GGPKContainer.ExportAsync(list, bkg.NextProgress).ContinueWith((tsk) => {
                            if (tsk.Result == null)
                                MessageBox.Show("Exported " + list.Count.ToString() + " Files", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                            else
                                Dispatcher.Invoke(() => { throw tsk.Result; });
                            bkg.Close();
                        });
                        bkg.ShowDialog();
                    }
                }
            }
        }

        private void OnReplaceClicked(object sender, RoutedEventArgs e)
        {
            if ((Tree.SelectedItem as TreeViewItem)?.Tag is RecordTreeNode rtn)
            {
                if (rtn is IFileRecord fr)
                {
                    var ofd = new OpenFileDialog { FileName = rtn.Name };
                    if (ofd.ShowDialog() == true)
                    {
                        fr.ReplaceContent(File.ReadAllBytes(ofd.FileName));
                        MessageBox.Show("Replaced " + rtn.GetPath(), "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    var ofd = new OpenFolderDialog();
                    if (ofd.ShowDialog() == true)
                    {
                        var list = new Collection<KeyValuePair<IFileRecord, string>>();
                        GGPKContainer.RecursiveFileList(rtn, ofd.DirectoryPath, list, false);
                        var bkg = new BackgroundDialog { ProgressText = "Replaced {0}/" + list.Count.ToString() + " Files . . ." };
                        ggpkContainer.ReplaceAsync(list, bkg.NextProgress).ContinueWith((tsk) => {
                            if (tsk.Result == null)
                                MessageBox.Show("Replaced " + list.Count.ToString() + " Files", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                            else
                                Dispatcher.Invoke(() => { throw tsk.Result; });
                            bkg.Close();
                        });
                        bkg.ShowDialog();
                    }
                }
            }
        }

        private void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            if (Tree.SelectedItem is TreeViewItem tvi && tvi.Tag is IFileRecord fr)
            {
                if ((string)TextView.Tag == "Unicode") {
                    fr.ReplaceContent(Unicode.GetBytes("\xFEFF" + TextView.Text));
                } else if ((string)TextView.Tag == "UTF8") {
                    fr.ReplaceContent(UTF8.GetBytes(TextView.Text));
                } else
                    return; 
                MessageBox.Show("Saved to " + ((RecordTreeNode)fr).GetPath(), "Done", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OnSavePngClicked(object sender, RoutedEventArgs e) {
            var sfd = new SaveFileDialog { FileName = ((string)ImageView.Tag).Replace("dds", "png") };
            if (sfd.ShowDialog() == true) {
                var pbe = new PngBitmapEncoder();
                pbe.Frames.Add(BitmapFrame.Create((BitmapSource)ImageView.Source));
                var f = File.OpenWrite(sfd.FileName);
                pbe.Save(f);
                f.Flush();
                f.Close();
                MessageBox.Show("Saved " + sfd.FileName, "Done", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
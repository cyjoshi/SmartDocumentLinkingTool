using Microsoft.Win32;
using System.Windows;

namespace FormRecTesting
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

        }
        [System.Runtime.InteropServices.DllImport("Kernel32")]
        public static extern void AllocConsole();

        [System.Runtime.InteropServices.DllImport("Kernel32")]
        public static extern void FreeConsole();
        private async void uploadFile_Click(object sender, RoutedEventArgs e)
        {
            //AllocConsole();
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.ShowDialog();
            string filePath = ofd.FileName;
            await Program.processFile(filePath);
            MessageBox.Show("File processed Successfully!");
            //FreeConsole();
            //Application.Current.Shutdown();
        }
    }
}

using Velopack;

namespace FoxAutoClicker;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Kurulum/güncelleme kancalarını işler; normal açılışta hemen geri döner
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}

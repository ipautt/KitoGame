using Microsoft.Extensions.Logging;
using KitoGame.Engine;
using KitoGame.Pages;
using KitoGame.Services;

namespace KitoGame
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Registro de Servicios Escalables y Motor de Juegos
            builder.Services.AddSingleton<LocalizationService>();
            builder.Services.AddSingleton<GameRegistry>();
            builder.Services.AddSingleton<AppStateService>();

            // Registro de Páginas
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<LobbyPage>();
            builder.Services.AddTransient<ProfilePage>();
            builder.Services.AddTransient<WalletPage>();
            builder.Services.AddTransient<AdminPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}


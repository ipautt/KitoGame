using KitoGame.Engine.Checkers;
using KitoGame.Engine.Chess;
using KitoGame.Models;

namespace KitoGame.Engine;

/// <summary>
/// Registro central escalable de juegos disponibles en la plataforma.
/// Para agregar un nuevo juego en el futuro, basta con registrar su fábrica aquí.
/// </summary>
public class GameRegistry
{
    private readonly Dictionary<string, Func<IGameEngine>> _factories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GameMetadata> _metadata = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IGameEngine> _activeMatchEngines = new(StringComparer.OrdinalIgnoreCase);

    public GameRegistry()
    {
        RegisterGame(() => new CheckersEngine());
        RegisterGame(() => new ChessEngine());
    }

    public void RegisterGame(Func<IGameEngine> engineFactory)
    {
        var sample = engineFactory();
        _factories[sample.Metadata.Id] = engineFactory;
        _metadata[sample.Metadata.Id] = sample.Metadata;
    }

    public IReadOnlyList<GameMetadata> GetAvailableGames() =>
        _metadata.Values.Where(g => g.IsEnabled).ToList();

    public GameMetadata? GetGameMetadata(string gameId) =>
        _metadata.TryGetValue(gameId, out var meta) ? meta : null;

    public IGameEngine GetOrCreateMatchEngine(string matchId, string gameId)
    {
        if (_activeMatchEngines.TryGetValue(matchId, out var existing))
        {
            return existing;
        }

        if (!_factories.TryGetValue(gameId, out var factory))
        {
            throw new InvalidOperationException($"El juego '{gameId}' no está registrado en GameRegistry.");
        }

        var engine = factory();
        engine.ResetBoard();
        _activeMatchEngines[matchId] = engine;
        return engine;
    }
}


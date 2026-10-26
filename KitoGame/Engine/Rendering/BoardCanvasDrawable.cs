using Microsoft.Maui.Graphics;

namespace KitoGame.Engine.Rendering;

public class BoardCanvasDrawable : IDrawable
{
    public IGameEngine? Engine { get; set; }
    public BoardPosition? SelectedCell { get; set; }
    public IReadOnlyList<BoardPosition> HighlightedMoves { get; set; } = Array.Empty<BoardPosition>();

    private const float BorderMargin = 20f;

    public BoardPosition? GetCellFromTouchPoint(PointF touchPoint, float viewWidth, float viewHeight)
    {
        float size = Math.Min(viewWidth, viewHeight);
        float boardSize = size - BorderMargin * 2;
        if (boardSize <= 0) return null;

        float cellSize = boardSize / 8f;
        float relX = touchPoint.X - BorderMargin;
        float relY = touchPoint.Y - BorderMargin;

        if (relX < 0 || relY < 0 || relX >= boardSize || relY >= boardSize)
            return null;

        int col = (int)(relX / cellSize);
        int row = (int)(relY / cellSize);

        var pos = new BoardPosition(row, col);
        return pos.IsInsideBoard() ? pos : null;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float size = Math.Min(dirtyRect.Width, dirtyRect.Height);
        if (size <= 40) return;

        float boardSize = size - BorderMargin * 2;
        float cellSize = boardSize / 8f;

        // 1. Marco exterior del tablero estilo madera/torneo
        canvas.FillColor = Color.FromArgb("#1E293B");
        canvas.FillRoundedRectangle(0, 0, size, size, 14f);

        canvas.StrokeColor = Color.FromArgb("#475569");
        canvas.StrokeSize = 2f;
        canvas.DrawRoundedRectangle(1, 1, size - 2, size - 2, 14f);

        // Coordenadas A-H y 1-8 en el marco
        canvas.FontColor = Color.FromArgb("#94A3B8");
        canvas.FontSize = 10f;
        for (int i = 0; i < 8; i++)
        {
            string colLetter = ((char)('A' + i)).ToString();
            float cx = BorderMargin + i * cellSize;
            canvas.DrawString(colLetter, cx, 0, cellSize, BorderMargin, HorizontalAlignment.Center, VerticalAlignment.Center);
            canvas.DrawString(colLetter, cx, size - BorderMargin, cellSize, BorderMargin, HorizontalAlignment.Center, VerticalAlignment.Center);

            string rowNumber = (8 - i).ToString();
            float ry = BorderMargin + i * cellSize;
            canvas.DrawString(rowNumber, 0, ry, BorderMargin, cellSize, HorizontalAlignment.Center, VerticalAlignment.Center);
            canvas.DrawString(rowNumber, size - BorderMargin, ry, BorderMargin, cellSize, HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        if (Engine == null) return;

        bool isCheckers = Engine.Metadata.Id.Equals("checkers", StringComparison.OrdinalIgnoreCase);
        bool isParchis = Engine.Metadata.Id.Equals("parchis", StringComparison.OrdinalIgnoreCase);

        // 2. Dibujar las 64 casillas
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                var pos = new BoardPosition(r, c);
                float x = BorderMargin + c * cellSize;
                float y = BorderMargin + r * cellSize;

                bool isDark = (r + c) % 2 == 1;

                if (isParchis)
                {
                    // Casas de Parchís en las esquinas y meta central
                    if (r >= 5 && c <= 2)
                        canvas.FillColor = isDark ? Color.FromArgb("#FCA5A5") : Color.FromArgb("#FEF2F2"); // Casa Roja (P1)
                    else if (r <= 2 && c >= 5)
                        canvas.FillColor = isDark ? Color.FromArgb("#86EFAC") : Color.FromArgb("#F0FDF4"); // Casa Verde (P2)
                    else if (r <= 2 && c <= 2)
                        canvas.FillColor = isDark ? Color.FromArgb("#FDE047") : Color.FromArgb("#FEFCE8"); // Casa Amarilla
                    else if (r >= 5 && c >= 5)
                        canvas.FillColor = isDark ? Color.FromArgb("#93C5FD") : Color.FromArgb("#EFF6FF"); // Casa Azul
                    else if ((r == 3 || r == 4) && (c == 3 || c == 4))
                        canvas.FillColor = Color.FromArgb("#F59E0B"); // Meta Central 🏆
                    else
                        canvas.FillColor = isDark ? Color.FromArgb("#CBD5E1") : Color.FromArgb("#F8FAFC"); // Pistas
                }
                else if (isCheckers)
                {
                    canvas.FillColor = isDark
                        ? Color.FromArgb("#273549")
                        : Color.FromArgb("#E2E8F0");
                }
                else
                {
                    canvas.FillColor = isDark
                        ? Color.FromArgb("#334155")
                        : Color.FromArgb("#F1F5F9");
                }

                canvas.FillRectangle(x, y, cellSize, cellSize);

                // Rastro del último movimiento realizado
                if ((Engine.LastMoveFrom.HasValue && Engine.LastMoveFrom.Value == pos) ||
                    (Engine.LastMoveTo.HasValue && Engine.LastMoveTo.Value == pos))
                {
                    canvas.FillColor = Color.FromRgba(59, 130, 246, 95);
                    canvas.FillRectangle(x, y, cellSize, cellSize);
                }

                // Ficha seleccionada o con salto obligatorio en cadena
                bool isSelected = (SelectedCell.HasValue && SelectedCell.Value == pos) ||
                                  (Engine.ForcedPiecePosition.HasValue && Engine.ForcedPiecePosition.Value == pos);

                if (isSelected)
                {
                    canvas.FillColor = Color.FromRgba(245, 158, 11, 130);
                    canvas.FillRectangle(x, y, cellSize, cellSize);
                    canvas.StrokeColor = Color.FromArgb("#FBBF24");
                    canvas.StrokeSize = 3f;
                    canvas.DrawRectangle(x + 1.5f, y + 1.5f, cellSize - 3f, cellSize - 3f);
                }

                // Casillas destino válidas (Indicador verde esmeralda)
                if (HighlightedMoves.Contains(pos))
                {
                    bool isJumpOrCapture = SelectedCell.HasValue &&
                                           (Math.Abs(pos.Row - SelectedCell.Value.Row) == 2 || Engine.GetPieceAt(pos) != null);

                    canvas.FillColor = isJumpOrCapture
                        ? Color.FromRgba(16, 185, 129, 130)
                        : Color.FromRgba(16, 185, 129, 90);
                    canvas.FillRectangle(x, y, cellSize, cellSize);

                    float centerX = x + cellSize / 2f;
                    float centerY = y + cellSize / 2f;

                    canvas.FillColor = Color.FromArgb("#10B981");
                    canvas.FillCircle(centerX, centerY, cellSize * 0.18f);

                    canvas.StrokeColor = Colors.White;
                    canvas.StrokeSize = 2f;
                    canvas.DrawCircle(centerX, centerY, cellSize * 0.28f);
                }

                // 3. Dibujar ficha o pieza si existe en esta casilla
                var piece = Engine.GetPieceAt(pos);
                if (piece != null)
                {
                    if (isParchis)
                    {
                        DrawParchisToken(canvas, x, y, cellSize, piece);
                    }
                    else if (isCheckers)
                    {
                        DrawCheckerDisc3D(canvas, x, y, cellSize, piece);
                    }
                    else
                    {
                        DrawChessPieceMedallion(canvas, x, y, cellSize, piece);
                    }
                }
            }
        }
    }

    private static void DrawParchisToken(ICanvas canvas, float x, float y, float cellSize, BoardPiece piece)
    {
        float cx = x + cellSize / 2f;
        float cy = y + cellSize / 2f;
        float radius = cellSize * 0.38f;

        bool isP1 = piece.Owner == PlayerSide.Player1;
        Color baseColor = isP1 ? Color.FromArgb("#EF4444") : Color.FromArgb("#10B981");
        Color darkColor = isP1 ? Color.FromArgb("#991B1B") : Color.FromArgb("#065F46");
        Color lightColor = isP1 ? Color.FromArgb("#FCA5A5") : Color.FromArgb("#A7F3D0");

        // Sombra
        canvas.FillColor = Color.FromRgba(0, 0, 0, 110);
        canvas.FillCircle(cx + 1.5f, cy + 2.5f, radius);

        // Base 3D
        canvas.FillColor = darkColor;
        canvas.FillCircle(cx, cy + 2f, radius);

        // Cuerpo superior
        canvas.FillColor = baseColor;
        canvas.FillCircle(cx, cy - 1f, radius * 0.92f);

        // Anillo de brillo
        canvas.StrokeColor = lightColor;
        canvas.StrokeSize = 2f;
        canvas.DrawCircle(cx, cy - 1f, radius * 0.82f);

        // Cúpula del peón de parchís
        canvas.FillColor = Colors.White;
        canvas.FillCircle(cx, cy - 1f, radius * 0.35f);

        canvas.FontColor = darkColor;
        canvas.FontSize = cellSize * 0.30f;
        canvas.DrawString(isP1 ? "🔴" : "🟢", cx - radius, cy - radius - 1f, radius * 2, radius * 2, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    private static void DrawCheckerDisc3D(ICanvas canvas, float x, float y, float cellSize, BoardPiece piece)
    {
        float cx = x + cellSize / 2f;
        float cy = y + cellSize / 2f;
        float radius = cellSize * 0.39f;

        bool isPlayer1 = piece.Owner == PlayerSide.Player1;

        // Sombra proyectada inferior
        canvas.FillColor = Color.FromRgba(0, 0, 0, 130);
        canvas.FillCircle(cx + 1.5f, cy + 3f, radius);

        if (!piece.IsKingOrPromoted)
        {
            // --- FICHA NORMAL (PEÓN DE DAMA) ---
            canvas.FillColor = isPlayer1
                ? Color.FromArgb("#991B1B") // Base roja oscura
                : Color.FromArgb("#090D16"); // Base negra
            canvas.FillCircle(cx, cy + 1.5f, radius);

            canvas.FillColor = isPlayer1
                ? Color.FromArgb("#EF4444") // Cara Roja Rubí (Tuya)
                : Color.FromArgb("#1E293B"); // Cara Negra/Pizarra (Rival)
            canvas.FillCircle(cx, cy - 1f, radius * 0.94f);

            canvas.StrokeColor = isPlayer1
                ? Color.FromArgb("#FCA5A5")
                : Color.FromArgb("#94A3B8");
            canvas.StrokeSize = 2f;
            canvas.DrawCircle(cx, cy - 1f, radius * 0.88f);

            canvas.StrokeColor = isPlayer1
                ? Color.FromArgb("#B91C1C")
                : Color.FromArgb("#0F172A");
            canvas.StrokeSize = 2f;
            canvas.DrawCircle(cx, cy - 1f, radius * 0.60f);

            canvas.FillColor = Color.FromRgba(255, 255, 255, 30);
            canvas.FillCircle(cx - radius * 0.15f, cy - radius * 0.22f, radius * 0.40f);
        }
        else if (isPlayer1)
        {
            // --- TU FICHA CORONADA (REINA JUGADOR 1: ROJO ESCARLATA + BORDE DORADO/ESMERALDA + ★) ---
            // Disco base doble en Oro
            canvas.FillColor = Color.FromArgb("#D97706");
            canvas.FillCircle(cx, cy + 2f, radius * 1.03f);

            // Cuerpo principal Rojo Escarlata Vivo
            canvas.FillColor = Color.FromArgb("#DC2626");
            canvas.FillCircle(cx, cy - 1.5f, radius * 0.95f);

            // Doble anillo exterior en Oro brillante (#FDE047) y Verde Esmeralda (#10B981)
            canvas.StrokeColor = Color.FromArgb("#FDE047");
            canvas.StrokeSize = 3f;
            canvas.DrawCircle(cx, cy - 1.5f, radius * 0.88f);

            canvas.StrokeColor = Color.FromArgb("#10B981");
            canvas.StrokeSize = 2f;
            canvas.DrawCircle(cx, cy - 1.5f, radius * 0.68f);

            // Centro dorado con estrella blanca/roja distintiva de TU REINA
            canvas.FillColor = Color.FromArgb("#F59E0B");
            canvas.FillCircle(cx, cy - 1.5f, radius * 0.48f);

            canvas.FontColor = Color.FromArgb("#FFFFFF");
            canvas.FontSize = cellSize * 0.34f;
            canvas.DrawString("★", x, y - 2.5f, cellSize, cellSize, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
        else
        {
            // --- FICHA CORONADA DEL CONTRINCANTE (REINA RIVAL: MORADO VIOLETA + BORDE AZUL CIAN + ♛) ---
            // Disco base doble en Azul Cian Oscuro
            canvas.FillColor = Color.FromArgb("#0284C7");
            canvas.FillCircle(cx, cy + 2f, radius * 1.03f);

            // Cuerpo principal Morado / Violeta Intenso (Color 100% diferente al rojo tuyo)
            canvas.FillColor = Color.FromArgb("#7C3AED");
            canvas.FillCircle(cx, cy - 1.5f, radius * 0.95f);

            // Doble anillo exterior en Azul Cian Brillante (#38BDF8) y Plata
            canvas.StrokeColor = Color.FromArgb("#38BDF8");
            canvas.StrokeSize = 3f;
            canvas.DrawCircle(cx, cy - 1.5f, radius * 0.88f);

            canvas.StrokeColor = Color.FromArgb("#C084FC");
            canvas.StrokeSize = 2f;
            canvas.DrawCircle(cx, cy - 1.5f, radius * 0.68f);

            // Centro oscuro con símbolo cian distintivo de REINA RIVAL
            canvas.FillColor = Color.FromArgb("#3B0764");
            canvas.FillCircle(cx, cy - 1.5f, radius * 0.48f);

            canvas.FontColor = Color.FromArgb("#38BDF8");
            canvas.FontSize = cellSize * 0.32f;
            canvas.DrawString("♛", x, y - 2.5f, cellSize, cellSize, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }

    private static void DrawChessPieceMedallion(ICanvas canvas, float x, float y, float cellSize, BoardPiece piece)
    {
        float cx = x + cellSize / 2f;
        float cy = y + cellSize / 2f;
        float radius = cellSize * 0.40f;

        bool isPlayer1 = piece.Owner == PlayerSide.Player1;

        canvas.FillColor = Color.FromRgba(0, 0, 0, 100);
        canvas.FillCircle(cx + 1f, cy + 2f, radius);

        canvas.FillColor = isPlayer1
            ? Color.FromArgb("#F8FAFC")
            : Color.FromArgb("#0F172A");
        canvas.FillCircle(cx, cy, radius);

        canvas.StrokeColor = isPlayer1
            ? Color.FromArgb("#3B82F6")
            : Color.FromArgb("#F59E0B");
        canvas.StrokeSize = 2f;
        canvas.DrawCircle(cx, cy, radius * 0.92f);

        canvas.FontColor = isPlayer1
            ? Color.FromArgb("#0F172A")
            : Color.FromArgb("#FBBF24");
        canvas.FontSize = cellSize * 0.48f;
        canvas.DrawString(piece.Symbol, x, y, cellSize, cellSize, HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}


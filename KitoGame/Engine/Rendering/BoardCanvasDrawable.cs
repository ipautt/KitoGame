using KitoGame.Engine.Parchis;
using Microsoft.Maui.Graphics;

namespace KitoGame.Engine.Rendering;

public class BoardCanvasDrawable : IDrawable
{
    public IGameEngine? Engine { get; set; }
    public BoardPosition? SelectedCell { get; set; }
    public IReadOnlyList<BoardPosition> HighlightedMoves { get; set; } = Array.Empty<BoardPosition>();

    private const float BorderMargin = 16f;

    public BoardPosition? GetCellFromTouchPoint(PointF touchPoint, float viewWidth, float viewHeight)
    {
        float size = Math.Min(viewWidth, viewHeight);
        float boardSize = size - BorderMargin * 2;
        if (boardSize <= 0) return null;

        int cols = Engine?.Cols ?? 8;
        int rows = Engine?.Rows ?? 8;

        float cellSize = boardSize / (float)cols;
        float relX = touchPoint.X - BorderMargin;
        float relY = touchPoint.Y - BorderMargin;

        if (relX < 0 || relY < 0 || relX >= boardSize || relY >= boardSize)
            return null;

        int col = (int)(relX / cellSize);
        int row = (int)(relY / cellSize);

        var pos = new BoardPosition(row, col);
        return pos.IsInsideBoard(rows, cols) ? pos : null;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float size = Math.Min(dirtyRect.Width, dirtyRect.Height);
        if (size <= 40) return;

        if (Engine == null) return;

        bool isParchis = Engine.Metadata.Id.Equals("parchis", StringComparison.OrdinalIgnoreCase);
        if (isParchis)
        {
            DrawParchisBoard(canvas, size);
            return;
        }

        // --- TABLERO 8x8 (AJEDREZ Y DAMAS) ---
        float boardSize = size - BorderMargin * 2;
        float cellSize = boardSize / 8f;

        // Marco exterior estilo torneo
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

        bool isCheckers = Engine.Metadata.Id.Equals("checkers", StringComparison.OrdinalIgnoreCase);

        // Dibujar las 64 casillas
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                var pos = new BoardPosition(r, c);
                float x = BorderMargin + c * cellSize;
                float y = BorderMargin + r * cellSize;
                bool isDark = (r + c) % 2 == 1;

                canvas.FillColor = isCheckers
                    ? (isDark ? Color.FromArgb("#273549") : Color.FromArgb("#E2E8F0"))
                    : (isDark ? Color.FromArgb("#334155") : Color.FromArgb("#F1F5F9"));

                canvas.FillRectangle(x, y, cellSize, cellSize);

                // Rastro del último movimiento
                if ((Engine.LastMoveFrom.HasValue && Engine.LastMoveFrom.Value == pos) ||
                    (Engine.LastMoveTo.HasValue && Engine.LastMoveTo.Value == pos))
                {
                    canvas.FillColor = Color.FromRgba(59, 130, 246, 95);
                    canvas.FillRectangle(x, y, cellSize, cellSize);
                }

                // Ficha seleccionada
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

                // Destinos válidos
                if (HighlightedMoves.Contains(pos))
                {
                    canvas.FillColor = Color.FromRgba(16, 185, 129, 120);
                    canvas.FillRectangle(x, y, cellSize, cellSize);

                    float centerX = x + cellSize / 2f;
                    float centerY = y + cellSize / 2f;

                    canvas.FillColor = Color.FromArgb("#10B981");
                    canvas.FillCircle(centerX, centerY, cellSize * 0.18f);

                    canvas.StrokeColor = Colors.White;
                    canvas.StrokeSize = 2f;
                    canvas.DrawCircle(centerX, centerY, cellSize * 0.28f);
                }

                // Dibujar pieza
                var piece = Engine.GetPieceAt(pos);
                if (piece != null)
                {
                    if (isCheckers)
                        DrawCheckerDisc3D(canvas, x, y, cellSize, piece);
                    else
                        DrawChessPieceMedallion(canvas, x, y, cellSize, piece);
                }
            }
        }
    }

    // =========================================================================
    // RENDERIZADO DEL TABLERO REAL DE PARCHÍS (15x15)
    // =========================================================================
    private void DrawParchisBoard(ICanvas canvas, float size)
    {
        float boardSize = size - BorderMargin * 2;
        float cellSize = boardSize / 15f;

        // 1. Marco exterior de madera de lujo para Parchís
        canvas.FillColor = Color.FromArgb("#0F172A");
        canvas.FillRoundedRectangle(0, 0, size, size, 16f);

        canvas.StrokeColor = Color.FromArgb("#F59E0B");
        canvas.StrokeSize = 2.5f;
        canvas.DrawRoundedRectangle(1, 1, size - 2, size - 2, 16f);

        // Fondo del tablero en pergamino suave
        canvas.FillColor = Color.FromArgb("#FFFDF5");
        canvas.FillRoundedRectangle(BorderMargin, BorderMargin, boardSize, boardSize, 10f);

        // 2. Las 4 Casas de las Esquinas (6x6 celdas cada una)
        DrawCornerHouse(canvas, BorderMargin, BorderMargin, cellSize * 6, "AMARILLO", Color.FromArgb("#FEF9C3"), Color.FromArgb("#EAB308"), ParchisEngine.YellowJailSlots);
        DrawCornerHouse(canvas, BorderMargin + 9 * cellSize, BorderMargin, cellSize * 6, "VERDE", Color.FromArgb("#DCFCE7"), Color.FromArgb("#16A34A"), ParchisEngine.GreenJailSlots);
        DrawCornerHouse(canvas, BorderMargin, BorderMargin + 9 * cellSize, cellSize * 6, "ROJO", Color.FromArgb("#FEE2E2"), Color.FromArgb("#DC2626"), ParchisEngine.RedJailSlots);
        DrawCornerHouse(canvas, BorderMargin + 9 * cellSize, BorderMargin + 9 * cellSize, cellSize * 6, "AZUL", Color.FromArgb("#DBEAFE"), Color.FromArgb("#2563EB"), ParchisEngine.BlueJailSlots);

        // 3. Dibujar las Casillas del Circuito y Pasillos (15x15)
        for (int r = 0; r < 15; r++)
        {
            for (int c = 0; c < 15; c++)
            {
                // Omitir las 4 esquinas de 6x6 y la meta central 3x3 (se dibujan especialmente)
                bool isCorner = (r < 6 && c < 6) || (r < 6 && c >= 9) || (r >= 9 && c < 6) || (r >= 9 && c >= 9);
                bool isCenterGoal = (r >= 6 && r <= 8 && c >= 6 && c <= 8);
                if (isCorner || isCenterGoal) continue;

                var pos = new BoardPosition(r, c);
                float x = BorderMargin + c * cellSize;
                float y = BorderMargin + r * cellSize;

                // Color por defecto de casilla
                Color cellFill = Color.FromArgb("#FFFFFF");
                Color cellStroke = Color.FromArgb("#CBD5E1");

                // Identificar pasillos de llegada coloreados
                if (c == 7 && r >= 9 && r <= 13) // Pasillo Rojo
                {
                    cellFill = Color.FromArgb("#F87171");
                    cellStroke = Color.FromArgb("#DC2626");
                }
                else if (c == 7 && r >= 1 && r <= 5) // Pasillo Verde
                {
                    cellFill = Color.FromArgb("#4ADE80");
                    cellStroke = Color.FromArgb("#16A34A");
                }
                else if (r == 7 && c >= 1 && c <= 5) // Pasillo Amarillo
                {
                    cellFill = Color.FromArgb("#FBBF24");
                    cellStroke = Color.FromArgb("#D97706");
                }
                else if (r == 7 && c >= 9 && c <= 13) // Pasillo Azul
                {
                    cellFill = Color.FromArgb("#60A5FA");
                    cellStroke = Color.FromArgb("#2563EB");
                }
                // Casillas de Salida
                else if (r == 13 && c == 6) // Salida Roja
                {
                    cellFill = Color.FromArgb("#EF4444");
                    cellStroke = Color.FromArgb("#B91C1C");
                }
                else if (r == 1 && c == 8) // Salida Verde
                {
                    cellFill = Color.FromArgb("#10B981");
                    cellStroke = Color.FromArgb("#047857");
                }
                else if (r == 6 && c == 1) // Salida Amarilla
                {
                    cellFill = Color.FromArgb("#F59E0B");
                    cellStroke = Color.FromArgb("#B45309");
                }
                else if (r == 8 && c == 13) // Salida Azul
                {
                    cellFill = Color.FromArgb("#3B82F6");
                    cellStroke = Color.FromArgb("#1D4ED8");
                }
                // Seguros en extremos
                else if ((r == 7 && c == 0) || (r == 0 && c == 7) || (r == 7 && c == 14) || (r == 14 && c == 7))
                {
                    cellFill = Color.FromArgb("#F1F5F9");
                    cellStroke = Color.FromArgb("#94A3B8");
                }

                canvas.FillColor = cellFill;
                canvas.FillRoundedRectangle(x + 0.5f, y + 0.5f, cellSize - 1f, cellSize - 1f, 3f);

                canvas.StrokeColor = cellStroke;
                canvas.StrokeSize = 1.2f;
                canvas.DrawRoundedRectangle(x + 0.5f, y + 0.5f, cellSize - 1f, cellSize - 1f, 3f);

                // Estrella o símbolo en seguros y salidas
                if ((r == 13 && c == 6) || (r == 1 && c == 8) || (r == 6 && c == 1) || (r == 8 && c == 13) ||
                    (r == 7 && c == 0) || (r == 0 && c == 7) || (r == 7 && c == 14) || (r == 14 && c == 7))
                {
                    canvas.FontColor = Colors.White;
                    canvas.FontSize = cellSize * 0.45f;
                    canvas.DrawString("★", x, y, cellSize, cellSize, HorizontalAlignment.Center, VerticalAlignment.Center);
                }

                // Resaltado de celda seleccionada
                if (SelectedCell.HasValue && SelectedCell.Value == pos)
                {
                    canvas.StrokeColor = Color.FromArgb("#F59E0B");
                    canvas.StrokeSize = 2.5f;
                    canvas.DrawRoundedRectangle(x + 1f, y + 1f, cellSize - 2f, cellSize - 2f, 3f);
                }

                // Resaltado de movimientos válidos
                if (HighlightedMoves.Contains(pos))
                {
                    canvas.FillColor = Color.FromRgba(16, 185, 129, 140);
                    canvas.FillCircle(x + cellSize / 2f, y + cellSize / 2f, cellSize * 0.35f);

                    canvas.StrokeColor = Colors.White;
                    canvas.StrokeSize = 2f;
                    canvas.DrawCircle(x + cellSize / 2f, y + cellSize / 2f, cellSize * 0.35f);
                }
            }
        }

        // 4. Meta Central (3x3 celdas: de row 6..8, col 6..8) con 4 triángulos convergentes
        DrawParchisCenterGoal(canvas, BorderMargin + 6 * cellSize, BorderMargin + 6 * cellSize, cellSize * 3);

        // Si la meta es destino válido resaltado
        if (HighlightedMoves.Contains(ParchisEngine.CenterGoal))
        {
            float gcx = BorderMargin + 7.5f * cellSize;
            float gcy = BorderMargin + 7.5f * cellSize;
            canvas.StrokeColor = Color.FromArgb("#10B981");
            canvas.StrokeSize = 4f;
            canvas.DrawCircle(gcx, gcy, cellSize * 1.3f);
        }

        // 5. Dibujar todas las Fichas (Tokens) de Parchís en sus posiciones
        for (int r = 0; r < 15; r++)
        {
            for (int c = 0; c < 15; c++)
            {
                var pos = new BoardPosition(r, c);
                var piece = Engine.GetPieceAt(pos);
                if (piece != null)
                {
                    float px = BorderMargin + c * cellSize;
                    float py = BorderMargin + r * cellSize;
                    bool isSelected = SelectedCell.HasValue && SelectedCell.Value == pos;
                    DrawParchisToken(canvas, px, py, cellSize, piece, isSelected);
                }
            }
        }
    }

    private static void DrawCornerHouse(ICanvas canvas, float x, float y, float size, string label, Color bg, Color border, BoardPosition[] jailSlots)
    {
        // Cuadrado con bordes suaves de la Casa
        canvas.FillColor = bg;
        canvas.FillRoundedRectangle(x + 2, y + 2, size - 4, size - 4, 12f);

        canvas.StrokeColor = border;
        canvas.StrokeSize = 3f;
        canvas.DrawRoundedRectangle(x + 2, y + 2, size - 4, size - 4, 12f);

        // Patio interior circular en blanco marfil
        float cx = x + size / 2f;
        float cy = y + size / 2f;
        float yardRadius = size * 0.36f;

        canvas.FillColor = Colors.White;
        canvas.FillCircle(cx, cy, yardRadius);

        canvas.StrokeColor = border;
        canvas.StrokeSize = 2f;
        canvas.DrawCircle(cx, cy, yardRadius);

        // Etiqueta de la Casa
        canvas.FontColor = border;
        canvas.FontSize = 10f;
        canvas.DrawString(label, x, y + 4, size, 14, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    private static void DrawParchisCenterGoal(ICanvas canvas, float gx, float gy, float gSize)
    {
        float cx = gx + gSize / 2f;
        float cy = gy + gSize / 2f;

        // Triángulo Superior (Amarillo)
        var pathTop = new PathF();
        pathTop.MoveTo(gx, gy);
        pathTop.LineTo(gx + gSize, gy);
        pathTop.LineTo(cx, cy);
        pathTop.Close();
        canvas.FillColor = Color.FromArgb("#F59E0B");
        canvas.FillPath(pathTop);

        // Triángulo Derecho (Verde)
        var pathRight = new PathF();
        pathRight.MoveTo(gx + gSize, gy);
        pathRight.LineTo(gx + gSize, gy + gSize);
        pathRight.LineTo(cx, cy);
        pathRight.Close();
        canvas.FillColor = Color.FromArgb("#10B981");
        canvas.FillPath(pathRight);

        // Triángulo Inferior (Azul)
        var pathBottom = new PathF();
        pathBottom.MoveTo(gx + gSize, gy + gSize);
        pathBottom.LineTo(gx, gy + gSize);
        pathBottom.LineTo(cx, cy);
        pathBottom.Close();
        canvas.FillColor = Color.FromArgb("#3B82F6");
        canvas.FillPath(pathBottom);

        // Triángulo Izquierdo (Rojo)
        var pathLeft = new PathF();
        pathLeft.MoveTo(gx, gy + gSize);
        pathLeft.LineTo(gx, gy);
        pathLeft.LineTo(cx, cy);
        pathLeft.Close();
        canvas.FillColor = Color.FromArgb("#EF4444");
        canvas.FillPath(pathLeft);

        // Medallón Central Dorado con Trofeo
        canvas.FillColor = Color.FromArgb("#D97706");
        canvas.FillCircle(cx, cy, gSize * 0.22f);

        canvas.FillColor = Color.FromArgb("#FBBF24");
        canvas.FillCircle(cx, cy, gSize * 0.18f);

        canvas.FontColor = Color.FromArgb("#1E293B");
        canvas.FontSize = gSize * 0.22f;
        canvas.DrawString("🏆", cx - gSize * 0.2f, cy - gSize * 0.2f, gSize * 0.4f, gSize * 0.4f, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    private static void DrawParchisToken(ICanvas canvas, float x, float y, float cellSize, BoardPiece piece, bool isSelected = false)
    {
        float cx = x + cellSize / 2f;
        float cy = y + cellSize / 2f;
        float radius = cellSize * 0.42f;

        // Determinar colores de la ficha según su código o dueño
        Color baseColor, darkColor, lightColor;
        if (piece.Code.Contains("Red") || (piece.Owner == PlayerSide.Player1 && !piece.Code.Contains("Green")))
        {
            baseColor = Color.FromArgb("#EF4444");
            darkColor = Color.FromArgb("#991B1B");
            lightColor = Color.FromArgb("#FCA5A5");
        }
        else if (piece.Code.Contains("Yellow"))
        {
            baseColor = Color.FromArgb("#F59E0B");
            darkColor = Color.FromArgb("#78350F");
            lightColor = Color.FromArgb("#FEF08A");
        }
        else if (piece.Code.Contains("Blue"))
        {
            baseColor = Color.FromArgb("#3B82F6");
            darkColor = Color.FromArgb("#1E3A8A");
            lightColor = Color.FromArgb("#BFDBFE");
        }
        else
        {
            baseColor = Color.FromArgb("#10B981");
            darkColor = Color.FromArgb("#065F46");
            lightColor = Color.FromArgb("#A7F3D0");
        }

        // Anillo brillante si está seleccionada
        if (isSelected)
        {
            canvas.StrokeColor = Color.FromArgb("#F59E0B");
            canvas.StrokeSize = 3.5f;
            canvas.DrawCircle(cx, cy, radius * 1.35f);
        }

        // 1. Sombra 3D suave proyectada
        canvas.FillColor = Color.FromRgba(0, 0, 0, 110);
        canvas.FillCircle(cx + 1f, cy + 2f, radius);

        // 2. Base cilíndrica biselada (faldón inferior)
        canvas.FillColor = darkColor;
        canvas.FillCircle(cx, cy + 1.2f, radius);

        // 3. Anillo metálico dorado intermedio
        canvas.StrokeColor = Color.FromArgb("#FDE047");
        canvas.StrokeSize = 1.5f;
        canvas.DrawCircle(cx, cy + 0.5f, radius * 0.95f);

        // 4. Cuerpo principal vibrante
        canvas.FillColor = baseColor;
        canvas.FillCircle(cx, cy - 0.8f, radius * 0.88f);

        // 5. Reflejo especular blanco en la cabeza del peón
        canvas.FillColor = Color.FromRgba(255, 255, 255, 140);
        canvas.FillCircle(cx - radius * 0.25f, cy - radius * 0.35f, radius * 0.32f);

        // 6. Cúpula central
        canvas.FillColor = Colors.White;
        canvas.FillCircle(cx, cy - 0.8f, radius * 0.30f);

        canvas.FontColor = darkColor;
        canvas.FontSize = cellSize * 0.24f;
        canvas.DrawString("♟", cx - radius, cy - radius - 1f, radius * 2, radius * 2, HorizontalAlignment.Center, VerticalAlignment.Center);
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
            canvas.FillColor = isPlayer1
                ? Color.FromArgb("#991B1B")
                : Color.FromArgb("#090D16");
            canvas.FillCircle(cx, cy + 1.5f, radius);

            canvas.FillColor = isPlayer1
                ? Color.FromArgb("#EF4444")
                : Color.FromArgb("#1E293B");
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
            canvas.FillColor = Color.FromArgb("#D97706");
            canvas.FillCircle(cx, cy + 2f, radius * 1.03f);

            canvas.FillColor = Color.FromArgb("#DC2626");
            canvas.FillCircle(cx, cy - 1.5f, radius * 0.95f);

            canvas.StrokeColor = Color.FromArgb("#FDE047");
            canvas.StrokeSize = 3f;
            canvas.DrawCircle(cx, cy - 1.5f, radius * 0.88f);

            canvas.StrokeColor = Color.FromArgb("#10B981");
            canvas.StrokeSize = 2f;
            canvas.DrawCircle(cx, cy - 1.5f, radius * 0.68f);

            canvas.FillColor = Color.FromArgb("#F59E0B");
            canvas.FillCircle(cx, cy - 1.5f, radius * 0.48f);

            canvas.FontColor = Color.FromArgb("#FFFFFF");
            canvas.FontSize = cellSize * 0.34f;
            canvas.DrawString("★", x, y - 2.5f, cellSize, cellSize, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
        else
        {
            canvas.FillColor = Color.FromArgb("#0284C7");
            canvas.FillCircle(cx, cy + 2f, radius * 1.03f);

            canvas.FillColor = Color.FromArgb("#7C3AED");
            canvas.FillCircle(cx, cy - 1.5f, radius * 0.95f);

            canvas.StrokeColor = Color.FromArgb("#38BDF8");
            canvas.StrokeSize = 3f;
            canvas.DrawCircle(cx, cy - 1.5f, radius * 0.88f);

            canvas.StrokeColor = Color.FromArgb("#C084FC");
            canvas.StrokeSize = 2f;
            canvas.DrawCircle(cx, cy - 1.5f, radius * 0.68f);

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


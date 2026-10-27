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
    // RENDERIZADO DEL TABLERO TRADICIONAL DE PARQUÉS COLOMBIANO (15x15)
    // =========================================================================
    private void DrawParchisBoard(ICanvas canvas, float size)
    {
        float boardSize = size - BorderMargin * 2;
        float cellSize = boardSize / 15f;

        // 1. Sombra exterior del tablero completo para efecto tridimensional flotante
        canvas.FillColor = Color.FromRgba(0, 0, 0, 45);
        canvas.FillRoundedRectangle(BorderMargin + 4f, BorderMargin + 6f, boardSize, boardSize, 20f);

        // Fondo del tablero en blanco marfil pulido con marco grueso oscuro (como el tablero físico)
        canvas.FillColor = Color.FromArgb("#FFFFFF");
        canvas.FillRoundedRectangle(BorderMargin, BorderMargin, boardSize, boardSize, 18f);

        canvas.StrokeColor = Color.FromArgb("#0F172A");
        canvas.StrokeSize = 3.5f;
        canvas.DrawRoundedRectangle(BorderMargin, BorderMargin, boardSize, boardSize, 18f);

        // 2. Las 4 Casas / Cárceles Tradicionales con Iconos Culturales de Colombia (6x6 celdas cada una)
        // Arriba Izquierda: Amarillo (Chiva Colombiana)
        DrawCornerHouse(canvas, BorderMargin, BorderMargin, cellSize * 6, "Chiva", Color.FromArgb("#F59E0B"), Color.FromArgb("#B45309"), ParchisEngine.YellowJailSlots);
        // Arriba Derecha: Verde (Café de Colombia)
        DrawCornerHouse(canvas, BorderMargin + 9 * cellSize, BorderMargin, cellSize * 6, "Cafe", Color.FromArgb("#10B981"), Color.FromArgb("#047857"), ParchisEngine.GreenJailSlots);
        // Abajo Izquierda: Rojo (Acordeón Vallenato)
        DrawCornerHouse(canvas, BorderMargin, BorderMargin + 9 * cellSize, cellSize * 6, "Acordeon", Color.FromArgb("#EF4444"), Color.FromArgb("#B91C1C"), ParchisEngine.RedJailSlots);
        // Abajo Derecha: Azul (Sombrero Vueltiao)
        DrawCornerHouse(canvas, BorderMargin + 9 * cellSize, BorderMargin + 9 * cellSize, cellSize * 6, "Sombrero", Color.FromArgb("#3B82F6"), Color.FromArgb("#1D4ED8"), ParchisEngine.BlueJailSlots);

        // 3. Dibujar las Casillas del Circuito y Pasillos de Llegada
        for (int r = 0; r < 15; r++)
        {
            for (int c = 0; c < 15; c++)
            {
                // Omitir las 4 esquinas de 6x6 y la meta central 3x3
                bool isCorner = (r < 6 && c < 6) || (r < 6 && c >= 9) || (r >= 9 && c < 6) || (r >= 9 && c >= 9);
                bool isCenterGoal = (r >= 6 && r <= 8 && c >= 6 && c <= 8);
                if (isCorner || isCenterGoal) continue;

                var pos = new BoardPosition(r, c);
                float x = BorderMargin + c * cellSize;
                float y = BorderMargin + r * cellSize;

                // --- A) PASILLOS DE LLEGADA CON PASTILLAS / CÁPSULAS REDONDEADAS 3D ---
                bool isRedLane = (c == 7 && r >= 9 && r <= 13);
                bool isGreenLane = (c == 7 && r >= 1 && r <= 5);
                bool isYellowLane = (r == 7 && c >= 1 && c <= 5);
                bool isBlueLane = (r == 7 && c >= 9 && c <= 13);

                if (isRedLane || isGreenLane || isYellowLane || isBlueLane)
                {
                    // Fondo blanco de base
                    canvas.FillColor = Color.FromArgb("#FFFFFF");
                    canvas.FillRectangle(x, y, cellSize, cellSize);
                    canvas.StrokeColor = Color.FromArgb("#E2E8F0");
                    canvas.StrokeSize = 1f;
                    canvas.DrawRectangle(x, y, cellSize, cellSize);

                    // Pastilla coloreada con relieve y brillo
                    Color pillBase, pillDark, pillLight;
                    if (isRedLane)
                    {
                        pillBase = Color.FromArgb("#EF4444");
                        pillDark = Color.FromArgb("#B91C1C");
                        pillLight = Color.FromArgb("#FCA5A5");
                    }
                    else if (isGreenLane)
                    {
                        pillBase = Color.FromArgb("#10B981");
                        pillDark = Color.FromArgb("#047857");
                        pillLight = Color.FromArgb("#6EE7B7");
                    }
                    else if (isYellowLane)
                    {
                        pillBase = Color.FromArgb("#F59E0B");
                        pillDark = Color.FromArgb("#B45309");
                        pillLight = Color.FromArgb("#FDE68A");
                    }
                    else
                    {
                        pillBase = Color.FromArgb("#3B82F6");
                        pillDark = Color.FromArgb("#1D4ED8");
                        pillLight = Color.FromArgb("#93C5FD");
                    }

                    // Sombra suave de la cápsula
                    canvas.FillColor = Color.FromRgba(0, 0, 0, 30);
                    canvas.FillRoundedRectangle(x + 2.5f, y + 3.5f, cellSize - 5f, cellSize - 6f, 8f);

                    // Cuerpo principal de la cápsula
                    canvas.FillColor = pillBase;
                    canvas.FillRoundedRectangle(x + 2f, y + 2f, cellSize - 4f, cellSize - 5f, 7f);

                    // Borde biselado
                    canvas.StrokeColor = pillDark;
                    canvas.StrokeSize = 1.5f;
                    canvas.DrawRoundedRectangle(x + 2f, y + 2f, cellSize - 4f, cellSize - 5f, 7f);

                    // Brillo superior traslúcido
                    canvas.FillColor = Color.FromRgba(255, 255, 255, 90);
                    canvas.FillRoundedRectangle(x + 4f, y + 3.5f, cellSize - 8f, (cellSize - 5f) * 0.35f, 4f);

                    // Resaltado si está seleccionado o si es movimiento válido
                    DrawMoveHighlights(canvas, pos, x, y, cellSize);
                    continue;
                }

                // --- B) CASILLAS BLANCAS DEL CIRCUITO ---
                canvas.FillColor = Color.FromArgb("#FFFFFF");
                canvas.FillRectangle(x, y, cellSize, cellSize);

                canvas.StrokeColor = Color.FromArgb("#1E293B");
                canvas.StrokeSize = 1.2f;
                canvas.DrawRectangle(x, y, cellSize, cellSize);

                // --- C) CASILLAS DE SALIDA CON TEXTO "SALIDA" ---
                bool isRedExit = (r == 13 && c == 6);
                bool isGreenExit = (r == 1 && c == 8);
                bool isYellowExit = (r == 6 && c == 1);
                bool isBlueExit = (r == 8 && c == 13);

                if (isRedExit || isGreenExit || isYellowExit || isBlueExit)
                {
                    Color exitColor = isRedExit ? Color.FromArgb("#EF4444")
                                    : isGreenExit ? Color.FromArgb("#10B981")
                                    : isYellowExit ? Color.FromArgb("#F59E0B")
                                    : Color.FromArgb("#3B82F6");

                    canvas.FillColor = exitColor;
                    canvas.FillRoundedRectangle(x + 1.5f, y + 1.5f, cellSize - 3f, cellSize - 3f, 4f);

                    canvas.FontColor = Colors.White;
                    canvas.FontSize = cellSize * 0.28f;
                    canvas.DrawString("SALIDA", x, y, cellSize, cellSize, HorizontalAlignment.Center, VerticalAlignment.Center);

                    DrawMoveHighlights(canvas, pos, x, y, cellSize);
                    continue;
                }

                // --- D) CASILLAS DE SEGURO CON TEXTO "SEGURO" ---
                bool isEndSafe = (r == 7 && c == 0) || (r == 0 && c == 7) || (r == 7 && c == 14) || (r == 14 && c == 7);
                bool isMidSafe = (r == 8 && c == 3) || (r == 3 && c == 6) || (r == 6 && c == 11) || (r == 11 && c == 8);

                if (isEndSafe || isMidSafe)
                {
                    Color safeBg = isEndSafe ? Color.FromArgb("#0F172A") : Color.FromArgb("#334155");
                    canvas.FillColor = safeBg;
                    canvas.FillRoundedRectangle(x + 1.5f, y + 1.5f, cellSize - 3f, cellSize - 3f, 4f);

                    canvas.FontColor = Colors.White;
                    canvas.FontSize = cellSize * 0.26f;
                    canvas.DrawString("SEGURO", x, y, cellSize, cellSize, HorizontalAlignment.Center, VerticalAlignment.Center);

                    DrawMoveHighlights(canvas, pos, x, y, cellSize);
                    continue;
                }

                // Resaltado si está seleccionado o es jugada válida
                DrawMoveHighlights(canvas, pos, x, y, cellSize);
            }
        }

        // 4. Meta Central ("El Cielo" con la Bandera y texto COLOMBIA)
        DrawParchisCenterGoal(canvas, BorderMargin + 6 * cellSize, BorderMargin + 6 * cellSize, cellSize * 3);

        // Si la meta es destino válido resaltado
        if (HighlightedMoves.Contains(ParchisEngine.CenterGoal))
        {
            float gcx = BorderMargin + 7.5f * cellSize;
            float gcy = BorderMargin + 7.5f * cellSize;
            canvas.StrokeColor = Color.FromArgb("#10B981");
            canvas.StrokeSize = 5f;
            canvas.DrawCircle(gcx, gcy, cellSize * 1.35f);
        }

        // 5. Dibujar todas las Fichas (Tokens) con perspectiva 3D Cónica y Sombra Proyectada
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

    private void DrawMoveHighlights(ICanvas canvas, BoardPosition pos, float x, float y, float cellSize)
    {
        if (SelectedCell.HasValue && SelectedCell.Value == pos)
        {
            canvas.StrokeColor = Color.FromArgb("#F59E0B");
            canvas.StrokeSize = 3.5f;
            canvas.DrawRoundedRectangle(x + 1f, y + 1f, cellSize - 2f, cellSize - 2f, 4f);
        }

        if (HighlightedMoves.Contains(pos))
        {
            canvas.FillColor = Color.FromRgba(16, 185, 129, 160);
            canvas.FillCircle(x + cellSize / 2f, y + cellSize / 2f, cellSize * 0.36f);

            canvas.StrokeColor = Colors.White;
            canvas.StrokeSize = 2.5f;
            canvas.DrawCircle(x + cellSize / 2f, y + cellSize / 2f, cellSize * 0.36f);
        }
    }

    private static void DrawCornerHouse(ICanvas canvas, float x, float y, float size, string emblemType, Color bg, Color border, BoardPosition[] jailSlots)
    {
        // Marco exterior de la Casa
        canvas.FillColor = bg;
        canvas.FillRoundedRectangle(x + 2, y + 2, size - 4, size - 4, 16f);

        canvas.StrokeColor = border;
        canvas.StrokeSize = 3f;
        canvas.DrawRoundedRectangle(x + 2, y + 2, size - 4, size - 4, 16f);

        // Patio interior circular en blanco
        float cx = x + size / 2f;
        float cy = y + size / 2f;
        float yardRadius = size * 0.38f;

        canvas.FillColor = Color.FromRgba(255, 255, 255, 220);
        canvas.FillCircle(cx, cy, yardRadius);

        canvas.StrokeColor = border;
        canvas.StrokeSize = 2f;
        canvas.DrawCircle(cx, cy, yardRadius);

        // Ilustración Cultural Colombiana según la Esquina
        switch (emblemType)
        {
            case "Chiva":
                DrawChivaEmblem(canvas, cx, cy, size * 0.60f);
                break;
            case "Cafe":
                DrawCoffeeEmblem(canvas, cx, cy, size * 0.60f);
                break;
            case "Acordeon":
                DrawAccordionEmblem(canvas, cx, cy, size * 0.60f);
                break;
            case "Sombrero":
                DrawSombreroVueltiaoEmblem(canvas, cx, cy, size * 0.60f);
                break;
        }

        // Anillos para las 4 fichas en la cárcel
        float slotOffset = size * 0.22f;
        PointF[] slotCoords = new PointF[]
        {
            new(cx - slotOffset, cy - slotOffset),
            new(cx + slotOffset, cy - slotOffset),
            new(cx - slotOffset, cy + slotOffset),
            new(cx + slotOffset, cy + slotOffset)
        };

        foreach (var pt in slotCoords)
        {
            canvas.FillColor = Color.FromRgba(0, 0, 0, 15);
            canvas.FillCircle(pt.X, pt.Y, size * 0.12f);
            canvas.StrokeColor = Color.FromRgba(255, 255, 255, 120);
            canvas.StrokeSize = 1.5f;
            canvas.DrawCircle(pt.X, pt.Y, size * 0.12f);
        }
    }

    // --- ILUSTRACIÓN DE LA CHIVA COLOMBIANA (BUS ESCALERA TRADICIONAL) ---
    private static void DrawChivaEmblem(ICanvas canvas, float cx, float cy, float s)
    {
        // Chasis del bus
        float w = s * 0.85f;
        float h = s * 0.55f;
        float x = cx - w / 2f;
        float y = cy - h / 2f + s * 0.05f;

        // Sombra inferior
        canvas.FillColor = Color.FromRgba(0, 0, 0, 50);
        canvas.FillRoundedRectangle(x + 2, y + h - 2, w, 8f, 4f);

        // Carrocería amarilla tradicional
        canvas.FillColor = Color.FromArgb("#FBBF24");
        canvas.FillRoundedRectangle(x, y + h * 0.25f, w, h * 0.75f, 6f);

        // Rayas decorativas tricolor colombianas
        canvas.FillColor = Color.FromArgb("#EF4444");
        canvas.FillRectangle(x, y + h * 0.55f, w, h * 0.12f);
        canvas.FillColor = Color.FromArgb("#3B82F6");
        canvas.FillRectangle(x, y + h * 0.67f, w, h * 0.10f);

        // Techo de madera con parrilla de equipaje
        canvas.FillColor = Color.FromArgb("#16A34A");
        canvas.FillRoundedRectangle(x - 2f, y, w + 4f, h * 0.25f, 4f);

        // Maletas / Cestas en el techo
        canvas.FillColor = Color.FromArgb("#DC2626");
        canvas.FillRoundedRectangle(cx - w * 0.35f, y - 6f, w * 0.3f, 7f, 2f);
        canvas.FillColor = Color.FromArgb("#D97706");
        canvas.FillRoundedRectangle(cx + w * 0.05f, y - 8f, w * 0.28f, 9f, 2f);

        // Ruedas oscuras con rines cromados
        float rw = s * 0.16f;
        canvas.FillColor = Color.FromArgb("#1E293B");
        canvas.FillCircle(x + w * 0.22f, y + h, rw * 0.5f);
        canvas.FillCircle(x + w * 0.78f, y + h, rw * 0.5f);
        canvas.FillColor = Color.FromArgb("#94A3B8");
        canvas.FillCircle(x + w * 0.22f, y + h, rw * 0.22f);
        canvas.FillCircle(x + w * 0.78f, y + h, rw * 0.22f);

        // Parabrisas y faros
        canvas.FillColor = Color.FromArgb("#93C5FD");
        canvas.FillRoundedRectangle(x + 4f, y + h * 0.30f, w * 0.38f, h * 0.22f, 3f);
        canvas.FillRoundedRectangle(x + w * 0.55f, y + h * 0.30f, w * 0.38f, h * 0.22f, 3f);

        // Rótulo "CHIVA"
        canvas.FontColor = Color.FromArgb("#0F172A");
        canvas.FontSize = s * 0.13f;
        canvas.DrawString("🚌 CHIVA", cx - w * 0.5f, y + h * 0.78f, w, h * 0.22f, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    // --- ILUSTRACIÓN DE CAFÉ DE COLOMBIA (COSTAL, TAZA Y GRANOS) ---
    private static void DrawCoffeeEmblem(ICanvas canvas, float cx, float cy, float s)
    {
        // Costal de cabuya (burlap sack)
        float sw = s * 0.65f;
        float sh = s * 0.70f;
        float sx = cx - sw * 0.40f;
        float sy = cy - sh * 0.55f;

        // Sombra del costal
        canvas.FillColor = Color.FromRgba(0, 0, 0, 45);
        canvas.FillEllipse(cx - sw * 0.1f, cy + sh * 0.35f, sw * 0.9f, sh * 0.25f);

        // Costal beige textura café
        canvas.FillColor = Color.FromArgb("#D4A373");
        canvas.FillRoundedRectangle(sx, sy, sw, sh, 10f);

        canvas.StrokeColor = Color.FromArgb("#8C5B32");
        canvas.StrokeSize = 2f;
        canvas.DrawRoundedRectangle(sx, sy, sw, sh, 10f);

        // Estampa "CAFÉ COLOMBIA"
        canvas.StrokeColor = Color.FromArgb("#78350F");
        canvas.StrokeSize = 1.2f;
        canvas.DrawRoundedRectangle(sx + 5f, sy + sh * 0.30f, sw - 10f, sh * 0.38f, 4f);

        canvas.FontColor = Color.FromArgb("#78350F");
        canvas.FontSize = s * 0.12f;
        canvas.DrawString("CAFÉ", sx + 5f, sy + sh * 0.33f, sw - 10f, sh * 0.18f, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.FontSize = s * 0.08f;
        canvas.DrawString("COLOMBIA", sx + 5f, sy + sh * 0.50f, sw - 10f, sh * 0.15f, HorizontalAlignment.Center, VerticalAlignment.Center);

        // Taza de café caliente al lado
        float tx = cx + s * 0.18f;
        float ty = cy + s * 0.10f;
        float tr = s * 0.22f;

        canvas.FillColor = Colors.White;
        canvas.FillRoundedRectangle(tx, ty, tr, tr * 0.85f, 6f);
        canvas.StrokeColor = Color.FromArgb("#16A34A");
        canvas.StrokeSize = 1.5f;
        canvas.DrawRoundedRectangle(tx, ty, tr, tr * 0.85f, 6f);

        // Café líquido oscuro
        canvas.FillColor = Color.FromArgb("#3B1808");
        canvas.FillEllipse(tx + 2f, ty + 2f, tr - 4f, tr * 0.32f);

        // Granos de café tostados dispersos
        canvas.FillColor = Color.FromArgb("#451A03");
        canvas.FillEllipse(cx - s * 0.25f, cy + s * 0.28f, s * 0.12f, s * 0.08f);
        canvas.FillEllipse(cx - s * 0.10f, cy + s * 0.34f, s * 0.11f, s * 0.07f);
    }

    // --- ILUSTRACIÓN DEL ACORDEÓN VALLENATO ---
    private static void DrawAccordionEmblem(ICanvas canvas, float cx, float cy, float s)
    {
        float aw = s * 0.85f;
        float ah = s * 0.65f;
        float ax = cx - aw / 2f;
        float ay = cy - ah / 2f;

        // Sombra
        canvas.FillColor = Color.FromRgba(0, 0, 0, 50);
        canvas.FillRoundedRectangle(ax + 3, ay + ah - 2, aw, 8f, 4f);

        // Caja de pitos izquierda (rojo perlado)
        canvas.FillColor = Color.FromArgb("#B91C1C");
        canvas.FillRoundedRectangle(ax, ay + 2, aw * 0.26f, ah - 4, 6f);

        // Teclado con teclas blancas y negras
        float kw = aw * 0.20f;
        canvas.FillColor = Colors.White;
        canvas.FillRectangle(ax + 3f, ay + 6f, kw, ah - 12f);
        canvas.StrokeColor = Color.FromArgb("#0F172A");
        canvas.StrokeSize = 1f;
        canvas.DrawRectangle(ax + 3f, ay + 6f, kw, ah - 12f);

        // Teclas negras
        canvas.FillColor = Color.FromArgb("#0F172A");
        for (int i = 0; i < 4; i++)
        {
            canvas.FillRectangle(ax + 3f, ay + 10f + i * (ah * 0.18f), kw * 0.55f, ah * 0.10f);
        }

        // Fuelle plegado (pliegues plateados/blancos en acordeón)
        float fx = ax + aw * 0.26f;
        float fw = aw * 0.48f;
        int folds = 6;
        float foldStep = fw / folds;

        for (int i = 0; i < folds; i++)
        {
            float fLeft = fx + i * foldStep;
            canvas.FillColor = (i % 2 == 0) ? Color.FromArgb("#F1F5F9") : Color.FromArgb("#CBD5E1");
            canvas.FillRectangle(fLeft, ay + 4f, foldStep, ah - 8f);

            canvas.StrokeColor = Color.FromArgb("#64748B");
            canvas.StrokeSize = 0.8f;
            canvas.DrawRectangle(fLeft, ay + 4f, foldStep, ah - 8f);
        }

        // Caja de bajos derecha (roja)
        float bx = fx + fw;
        canvas.FillColor = Color.FromArgb("#B91C1C");
        canvas.FillRoundedRectangle(bx, ay + 2, aw * 0.26f, ah - 4, 6f);

        // Botones de bajos redondos
        canvas.FillColor = Color.FromArgb("#FEF08A");
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 2; c++)
            {
                canvas.FillCircle(bx + 6f + c * 10f, ay + 12f + r * 14f, 3f);
            }
        }

        // Notas musicales flotando
        canvas.FontColor = Color.FromArgb("#F59E0B");
        canvas.FontSize = s * 0.15f;
        canvas.DrawString("🎵", cx - s * 0.35f, ay - s * 0.15f, s * 0.3f, s * 0.3f, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.DrawString("🎶", cx + s * 0.15f, ay - s * 0.12f, s * 0.3f, s * 0.3f, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    // --- ILUSTRACIÓN DEL SOMBRERO VUELTIAO ZENÚ ---
    private static void DrawSombreroVueltiaoEmblem(ICanvas canvas, float cx, float cy, float s)
    {
        float w = s * 0.95f;
        float h = s * 0.60f;

        // Sombra suave inclinada
        canvas.FillColor = Color.FromRgba(0, 0, 0, 50);
        canvas.FillEllipse(cx + 3, cy + 4, w * 0.95f, h * 0.85f);

        // Ala ancha ovalada exterior (fondo crema de caña flecha)
        canvas.FillColor = Color.FromArgb("#FEF9C3");
        canvas.FillEllipse(cx, cy, w, h);

        canvas.StrokeColor = Color.FromArgb("#0F172A");
        canvas.StrokeSize = 3f;
        canvas.DrawEllipse(cx, cy, w, h);

        // Anillos concéntricos negros del tejido Zenú
        canvas.StrokeColor = Color.FromArgb("#0F172A");
        canvas.StrokeSize = 2f;
        canvas.DrawEllipse(cx, cy, w * 0.88f, h * 0.86f);
        canvas.DrawEllipse(cx, cy, w * 0.76f, h * 0.74f);
        canvas.DrawEllipse(cx, cy, w * 0.64f, h * 0.62f);

        // Patrón geométrico de cruces / rombos negros tradicionales Zenú
        canvas.FillColor = Color.FromArgb("#0F172A");
        for (int i = 0; i < 12; i++)
        {
            double angle = i * (Math.PI * 2 / 12.0);
            float px = cx + (float)(Math.Cos(angle) * (w * 0.40f));
            float py = cy + (float)(Math.Sin(angle) * (h * 0.38f));
            canvas.FillCircle(px, py, 2.5f);
        }

        // Copa central del sombrero
        float cw = w * 0.46f;
        float ch = h * 0.44f;
        canvas.FillColor = Color.FromArgb("#FEF08A");
        canvas.FillEllipse(cx, cy - 2f, cw, ch);

        canvas.StrokeColor = Color.FromArgb("#0F172A");
        canvas.StrokeSize = 2.5f;
        canvas.DrawEllipse(cx, cy - 2f, cw, ch);

        // Cinta negra alrededor de la copa
        canvas.FillColor = Color.FromArgb("#0F172A");
        canvas.FillEllipse(cx, cy + ch * 0.15f, cw * 0.95f, ch * 0.35f);
    }

    // --- META CENTRAL: EL CIELO CON LA BANDERA DE COLOMBIA Y ARCOS DE LLEGADA ---
    private static void DrawParchisCenterGoal(ICanvas canvas, float gx, float gy, float gSize)
    {
        float cx = gx + gSize / 2f;
        float cy = gy + gSize / 2f;
        float r = gSize * 0.56f;

        // 1. Sombra exterior del medallón
        canvas.FillColor = Color.FromRgba(0, 0, 0, 45);
        canvas.FillCircle(cx + 2f, cy + 3f, r);

        // 2. Arcos / Pétalos de entrada desde los 4 brazos (Rojo, Verde, Amarillo, Azul)
        // Arriba (Verde)
        var pTop = new PathF();
        pTop.MoveTo(gx, gy);
        pTop.LineTo(gx + gSize, gy);
        pTop.LineTo(cx, cy);
        pTop.Close();
        canvas.FillColor = Color.FromArgb("#10B981");
        canvas.FillPath(pTop);

        // Derecha (Azul)
        var pRight = new PathF();
        pRight.MoveTo(gx + gSize, gy);
        pRight.LineTo(gx + gSize, gy + gSize);
        pRight.LineTo(cx, cy);
        pRight.Close();
        canvas.FillColor = Color.FromArgb("#3B82F6");
        canvas.FillPath(pRight);

        // Abajo (Rojo)
        var pBottom = new PathF();
        pBottom.MoveTo(gx + gSize, gy + gSize);
        pBottom.LineTo(gx, gy + gSize);
        pBottom.LineTo(cx, cy);
        pBottom.Close();
        canvas.FillColor = Color.FromArgb("#EF4444");
        canvas.FillPath(pBottom);

        // Izquierda (Amarillo)
        var pLeft = new PathF();
        pLeft.MoveTo(gx, gy + gSize);
        pLeft.LineTo(gx, gy);
        pLeft.LineTo(cx, cy);
        pLeft.Close();
        canvas.FillColor = Color.FromArgb("#F59E0B");
        canvas.FillPath(pLeft);

        // 3. Medallón Circular Tricolor de Colombia
        float flagR = gSize * 0.44f;
        float k = 0.55228475f * flagR;

        // Path del círculo de la bandera para recortar las franjas
        var flagCircle = new PathF();
        flagCircle.MoveTo(cx, cy - flagR);
        flagCircle.CurveTo(cx + k, cy - flagR, cx + flagR, cy - k, cx + flagR, cy);
        flagCircle.CurveTo(cx + flagR, cy + k, cx + k, cy + flagR, cx, cy + flagR);
        flagCircle.CurveTo(cx - k, cy + flagR, cx - flagR, cy + k, cx - flagR, cy);
        flagCircle.CurveTo(cx - flagR, cy - k, cx - k, cy - flagR, cx, cy - flagR);
        flagCircle.Close();

        canvas.SaveState();
        canvas.ClipPath(flagCircle);

        // Amarillo de Colombia (50% superior)
        canvas.FillColor = Color.FromArgb("#FCD116");
        canvas.FillRectangle(cx - flagR - 2f, cy - flagR - 2f, (flagR + 2f) * 2f, flagR + 2f);

        // Azul de Colombia (25% intermedio)
        canvas.FillColor = Color.FromArgb("#003893");
        canvas.FillRectangle(cx - flagR - 2f, cy, (flagR + 2f) * 2f, flagR * 0.5f);

        // Rojo de Colombia (25% inferior)
        canvas.FillColor = Color.FromArgb("#CE1126");
        canvas.FillRectangle(cx - flagR - 2f, cy + flagR * 0.5f, (flagR + 2f) * 2f, flagR * 0.5f + 2f);

        canvas.RestoreState();

        // Borde exterior negro grueso de lujo
        canvas.StrokeColor = Color.FromArgb("#0F172A");
        canvas.StrokeSize = 3.5f;
        canvas.DrawCircle(cx, cy, flagR);

        // Aro dorado biselado
        canvas.StrokeColor = Color.FromArgb("#FDE047");
        canvas.StrokeSize = 2.5f;
        canvas.DrawCircle(cx, cy, flagR - 1f);

        // 4. Texto Estilizado "COLOMBIA" en el Centro
        // Sombra del texto
        canvas.FontColor = Color.FromArgb("#0F172A");
        canvas.FontSize = gSize * 0.16f;
        canvas.DrawString("COLOMBIA", cx - flagR + 1f, cy - gSize * 0.08f + 1f, flagR * 2, gSize * 0.20f, HorizontalAlignment.Center, VerticalAlignment.Center);

        // Texto blanco en relieve
        canvas.FontColor = Colors.White;
        canvas.DrawString("COLOMBIA", cx - flagR, cy - gSize * 0.08f, flagR * 2, gSize * 0.20f, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    // =========================================================================
    // FICHA DE PARQUÉS 3D: PERSPECTIVA CÓNICA CON SOMBRA PROYECTADA (DROP SHADOW)
    // =========================================================================
    private static void DrawParchisToken(ICanvas canvas, float x, float y, float cellSize, BoardPiece piece, bool isSelected = false)
    {
        // Punto de apoyo en el suelo de la casilla (base)
        float bx = x + cellSize / 2f;
        float by = y + cellSize * 0.74f;

        // Dimensiones del cono tridimensional
        float baseWidth = cellSize * 0.44f;
        float baseHeight = cellSize * 0.20f;
        float coneHeight = cellSize * 0.58f;
        float neckWidth = cellSize * 0.20f;
        float neckY = by - coneHeight;
        float headRadius = cellSize * 0.18f;
        float headCenterY = neckY - headRadius * 0.65f;

        // Determinar colores vibrantes y sombras según el dueño
        Color mainColor, shadowColor, sheenColor, highlightColor;
        if (piece.Code.Contains("Red") || (piece.Owner == PlayerSide.Player1 && !piece.Code.Contains("Green")))
        {
            mainColor = Color.FromArgb("#DC2626");
            shadowColor = Color.FromArgb("#7F1D1D");
            sheenColor = Color.FromArgb("#F87171");
            highlightColor = Color.FromArgb("#FEE2E2");
        }
        else if (piece.Code.Contains("Yellow"))
        {
            mainColor = Color.FromArgb("#EAB308");
            shadowColor = Color.FromArgb("#78350F");
            sheenColor = Color.FromArgb("#FDE047");
            highlightColor = Color.FromArgb("#FEF9C3");
        }
        else if (piece.Code.Contains("Blue"))
        {
            mainColor = Color.FromArgb("#2563EB");
            shadowColor = Color.FromArgb("#1E3A8A");
            sheenColor = Color.FromArgb("#60A5FA");
            highlightColor = Color.FromArgb("#DBEAFE");
        }
        else
        {
            mainColor = Color.FromArgb("#16A34A");
            shadowColor = Color.FromArgb("#064E3B");
            sheenColor = Color.FromArgb("#4ADE80");
            highlightColor = Color.FromArgb("#DCFCE7");
        }

        // --- 1. AURA / HALO DE SELECCIÓN O TURNO ACTIVO ---
        if (isSelected)
        {
            canvas.FillColor = Color.FromRgba(245, 158, 11, 70);
            canvas.FillEllipse(bx, by, baseWidth * 1.8f, baseHeight * 1.8f);

            canvas.StrokeColor = Color.FromArgb("#F59E0B");
            canvas.StrokeSize = 3.5f;
            canvas.DrawEllipse(bx, by, baseWidth * 1.6f, baseHeight * 1.6f);
        }

        // --- 2. SOMBRA PROYECTADA OBLICUA (DROP SHADOW A 45°) ---
        // Sombra realista que se estira hacia la derecha y abajo
        float sOffsetLen = cellSize * 0.40f;
        float sTipX = bx + sOffsetLen * 0.70f;
        float sTipY = by + sOffsetLen * 0.35f;

        // Halo difuso suave
        var shadowPath = new PathF();
        shadowPath.MoveTo(bx - baseWidth * 0.45f, by);
        shadowPath.LineTo(bx + baseWidth * 0.45f, by);
        shadowPath.LineTo(sTipX + headRadius * 0.85f, sTipY);
        shadowPath.LineTo(sTipX - headRadius * 0.85f, sTipY);
        shadowPath.Close();

        canvas.FillColor = Color.FromRgba(0, 0, 0, 50);
        canvas.FillPath(shadowPath);
        canvas.FillCircle(sTipX, sTipY, headRadius * 0.95f);

        // Núcleo de sombra más denso
        canvas.FillColor = Color.FromRgba(0, 0, 0, 95);
        canvas.FillEllipse(bx + 2f, by + 1.5f, baseWidth * 0.85f, baseHeight * 0.85f);

        // --- 3. BASE CILÍNDRICA BISELADA DE LA FICHA ---
        // Borde de sombra inferior de la base
        canvas.FillColor = shadowColor;
        canvas.FillEllipse(bx, by + 1.5f, baseWidth, baseHeight);

        // Tapa de la base
        canvas.FillColor = mainColor;
        canvas.FillEllipse(bx, by, baseWidth, baseHeight);

        // Brillo fino en el perímetro frontal de la base
        canvas.StrokeColor = sheenColor;
        canvas.StrokeSize = 1.2f;
        canvas.DrawEllipse(bx, by, baseWidth * 0.96f, baseHeight * 0.90f);

        // --- 4. CUERPO CÓNICO ESTILIZADO (PEÓN TIPO CONO TRIDIMENSIONAL) ---
        // Silueta cónica que sube desde la base hasta el cuello
        var coneBody = new PathF();
        coneBody.MoveTo(bx - baseWidth * 0.48f, by);
        coneBody.LineTo(bx - neckWidth * 0.50f, neckY);
        coneBody.LineTo(bx + neckWidth * 0.50f, neckY);
        coneBody.LineTo(bx + baseWidth * 0.48f, by);
        coneBody.Close();

        // Relleno principal del cono
        canvas.FillColor = mainColor;
        canvas.FillPath(coneBody);

        // Sombra de volumen vertical (lado derecho oscuro)
        var coneShadow = new PathF();
        coneShadow.MoveTo(bx, neckY);
        coneShadow.LineTo(bx + neckWidth * 0.50f, neckY);
        coneShadow.LineTo(bx + baseWidth * 0.48f, by);
        coneShadow.LineTo(bx, by);
        coneShadow.Close();

        canvas.FillColor = Color.FromRgba(0, 0, 0, 60);
        canvas.FillPath(coneShadow);

        // Franja vertical de brillo especular (lado izquierdo)
        var coneHighlight = new PathF();
        coneHighlight.MoveTo(bx - neckWidth * 0.40f, neckY + 2f);
        coneHighlight.LineTo(bx - neckWidth * 0.15f, neckY + 2f);
        coneHighlight.LineTo(bx - baseWidth * 0.15f, by - 2f);
        coneHighlight.LineTo(bx - baseWidth * 0.40f, by - 2f);
        coneHighlight.Close();

        canvas.FillColor = Color.FromRgba(255, 255, 255, 80);
        canvas.FillPath(coneHighlight);

        // --- 5. CABEZA ESFÉRICA TRIDIMENSIONAL CON BRILLO DE VOLUMEN ---
        // Base de la esfera
        canvas.FillColor = mainColor;
        canvas.FillCircle(bx, headCenterY, headRadius);

        // Media luna de sombra (inferior derecha de la esfera)
        canvas.FillColor = Color.FromRgba(0, 0, 0, 70);
        canvas.FillCircle(bx + headRadius * 0.22f, headCenterY + headRadius * 0.22f, headRadius * 0.78f);

        // Color vivo medio
        canvas.FillColor = sheenColor;
        canvas.FillCircle(bx - headRadius * 0.12f, headCenterY - headRadius * 0.12f, headRadius * 0.70f);

        // Reflejo especular blanco (punto de luz 3D superior izquierdo)
        canvas.FillColor = Color.FromRgba(255, 255, 255, 210);
        canvas.FillCircle(bx - headRadius * 0.32f, headCenterY - headRadius * 0.32f, headRadius * 0.32f);

        // Contorno fino exterior
        canvas.StrokeColor = shadowColor;
        canvas.StrokeSize = 1.0f;
        canvas.DrawCircle(bx, headCenterY, headRadius);
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


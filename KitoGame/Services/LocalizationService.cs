namespace KitoGame.Services;

public enum AppLanguage
{
    Es, // Español
    En, // English
    Pt  // Português
}

/// <summary>
/// Servicio centralizado de idiomas (Español, Inglés y Portugués) con cambio en vivo.
/// </summary>
public class LocalizationService
{
    public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.Es;

    public event Action? LanguageChanged;

    private static readonly Dictionary<string, (string Es, string En, string Pt)> Translations = new()
    {
        // Login & Registro
        ["AppSubtitle"] = (
            "Plataforma de Retos 1v1 • Damas y Ajedrez",
            "1v1 Challenge Platform • Checkers & Chess",
            "Plataforma de Desafios 1v1 • Damas e Xadrez"),
        ["SelectLanguageLabel"] = (
            "🌐 Idioma / Language:",
            "🌐 Language / Idioma:",
            "🌐 Idioma / Language:"),
        ["TabLogin"] = ("Iniciar Sesión", "Sign In", "Entrar"),
        ["TabRegister"] = ("Registrar Usuario", "Sign Up", "Criar Conta"),
        ["FormLoginTitle"] = ("Ingresa a tu cuenta", "Sign in to your account", "Acesse sua conta"),
        ["FormRegisterTitle"] = ("Crear nuevo usuario", "Create a new account", "Criar novo usuário"),
        ["FirstNameLabel"] = ("Nombres *", "First Name *", "Nome *"),
        ["LastNameLabel"] = ("Apellidos *", "Last Name *", "Sobrenome *"),
        ["EmailLabel"] = ("Correo Electrónico *", "Email Address *", "E-mail *"),
        ["PhoneLabel"] = ("Teléfono / Celular (Opcional)", "Phone Number (Optional)", "Telefone / Celular (Opcional)"),
        ["UsernameOrEmailLabel"] = ("Usuario o Correo Electrónico", "Username or Email", "Usuário ou E-mail"),
        ["UsernameAliasLabel"] = ("Nombre de Usuario (Alias) *", "Username (Alias) *", "Nome de Usuário *"),
        ["PasswordLabel"] = ("Contraseña", "Password", "Senha"),
        ["BtnLoginSubmit"] = ("Iniciar Sesión", "Sign In", "Entrar"),
        ["BtnRegisterSubmit"] = ("Crear Cuenta", "Create Account", "Criar Conta"),

        // Lobby
        ["AvailableBalance"] = ("SALDO DISPONIBLE", "AVAILABLE BALANCE", "SALDO DISPONÍVEL"),
        ["LockedBalance"] = ("EN CUSTODIA (PARTIDA)", "IN ESCROW (MATCH)", "EM CUSTÓDIA (PARTIDA)"),
        ["BtnProfile"] = ("👤 Mi Perfil", "👤 My Profile", "👤 Meu Perfil"),
        ["BtnWallet"] = ("💳 Billetera", "💳 Wallet", "💳 Carteira"),
        ["BtnAdmin"] = ("⚙️ Comisión", "⚙️ Commission", "⚙️ Comissão"),
        ["BtnLogout"] = ("Salir", "Logout", "Sair"),
        ["CommissionZeroInfo"] = (
            "Comisión del sistema: 0% (El ganador lleva el 100% del pozo)",
            "System fee: 0% (Winner takes 100% of the pot)",
            "Comissão do sistema: 0% (Vencedor leva 100% do prêmio)"),
        ["CommissionActiveInfo"] = (
            "Comisión del sistema: {0}% (El ganador lleva el {1}% del pozo)",
            "System fee: {0}% (Winner takes {1}% of the pot)",
            "Comissão do sistema: {0}% (Vencedor leva {1}% do prêmio)"),
        ["Section1Games"] = ("🎮 1. JUEGOS DISPONIBLES", "🎮 1. AVAILABLE GAMES", "🎮 1. JOGOS DISPONÍVEIS"),
        ["BtnSelected"] = ("Seleccionado ✓", "Selected ✓", "Selecionado ✓"),
        ["BtnChoose"] = ("Elegir", "Select", "Escolher"),
        ["Section2CreateMatch"] = ("⚔️ 2. CREAR PARTIDA EN: {0}", "⚔️ 2. CREATE MATCH IN: {0}", "⚔️ 2. CRIAR PARTIDA EM: {0}"),
        ["BetPrompt"] = (
            "Selecciona o escribe el monto de tu apuesta (COP):",
            "Select or enter your bet amount (COP):",
            "Selecione ou digite o valor da sua aposta (COP):"),
        ["BtnStartMatchNow"] = (
            "🔥 Apostar e Iniciar Partida Ahora",
            "🔥 Place Bet & Start Match Now",
            "🔥 Apostar e Iniciar Partida Agora"),
        ["Section3ActiveChallenges"] = (
            "🏆 3. RETOS RECIBIDOS Y PARTIDAS EN CURSO",
            "🏆 3. INCOMING CHALLENGES & ACTIVE MATCHES",
            "🏆 3. DESAFIOS RECEBIDOS E PARTIDAS ATIVAS"),
        ["BtnReceiveChallenge"] = ("+ Recibir Reto", "+ Receive Challenge", "+ Receber Desafio"),
        ["BtnContinueMatch"] = (
            "▶️ Continuar Partida en el Tablero",
            "▶️ Resume Match on Board",
            "▶️ Continuar Partida no Tabuleiro"),
        ["BtnAcceptChallenge"] = (
            "✅ Aceptar Reto (Apostar {0}) y Jugar",
            "✅ Accept Challenge (Bet {0}) & Play",
            "✅ Aceitar Desafio (Apostar {0}) e Jogar"),
        ["Section4History"] = (
            "📜 4. HISTORIAL DE PARTIDAS FINALIZADAS",
            "📜 4. COMPLETED MATCHES HISTORY",
            "📜 4. HISTÓRICO DE PARTIDAS FINALIZADAS"),

        // Juegos
        ["GameCheckersName"] = ("Damas Clásicas", "Classic Checkers", "Damas Clássicas"),
        ["GameCheckersDesc"] = (
            "Duelo 1v1 en tablero 8x8. Captura todas las fichas rivales o bloquéalas.",
            "1v1 duel on an 8x8 board. Capture or block all opponent discs.",
            "Duelo 1v1 no tabuleiro 8x8. Capture ou bloqueie todas as peças rivais."),
        ["GameChessName"] = ("Ajedrez Competitivo", "Competitive Chess", "Xadrez Competitivo"),
        ["GameChessDesc"] = (
            "El clásico rey de la estrategia 1v1. Captura al Rey rival para llevarte el pozo.",
            "The classic 1v1 strategy game. Capture the rival King to win the pot.",
            "O clássico jogo de estratégia 1v1. Capture o Rei rival para levar o prêmio."),
        ["GameParchisName"] = ("Parchís Clásico", "Classic Parchis / Ludo", "Parchís / Ludo Clássico"),
        ["GameParchisDesc"] = (
            "El rey de los juegos de mesa. Lleva tus 4 fichas a la meta central con el dado. ¡Captura y avanza 20 casillas!",
            "The classic board game. Race your 4 pawns to the center goal with the dice. Capture to advance 20 spaces!",
            "O clássico jogo de tabuleiro. Leve seus 4 peões à meta central com o dado. Capture para avançar 20 casas!"),

        // Perfil
        ["ProfileTitle"] = ("Mi Perfil de Usuario", "My User Profile", "Meu Perfil de Usuário"),
        ["EditProfileHeader"] = ("✏️ EDITAR INFORMACIÓN DEL USUARIO", "✏️ EDIT USER INFORMATION", "✏️ EDITAR INFORMAÇÕES DO USUÁRIO"),
        ["NewPasswordLabel"] = (
            "Nueva Contraseña (dejar en blanco para mantener la actual)",
            "New Password (leave blank to keep current)",
            "Nova Senha (deixe em branco para manter a atual)"),
        ["BtnSaveProfile"] = ("💾 Guardar Cambios del Perfil", "💾 Save Profile Changes", "💾 Salvar Alterações do Perfil"),

        // Billetera
        ["WalletTitle"] = ("Mi Billetera y Pagos", "My Wallet & Payments", "Minha Carteira e Pagamentos"),
        ["WalletStep1"] = ("💳 1. SOLICITAR RECARGA DE SALDO", "💳 1. REQUEST BALANCE TOP-UP", "💳 1. SOLICITAR RECARGA DE SALDO"),
        ["WalletStep1Sub"] = (
            "El saldo solo se agregará a tu cuenta una vez que el pago sea confirmado:",
            "Balance will only be credited once your payment is confirmed:",
            "O saldo só será adicionado após a confirmação do pagamento:"),
        ["PaymentMethodLabel"] = ("Medio de Pago", "Payment Method", "Meio de Pagamento"),
        ["DepositAmountLabel"] = ("Monto a Recargar (COP)", "Top-Up Amount (COP)", "Valor da Recarga (COP)"),
        ["BtnCreatePaymentOrder"] = ("🧾 Generar Orden de Pago", "🧾 Generate Payment Order", "🧾 Gerar Ordem de Pagamento"),
        ["WalletStep2"] = ("⏳ 2. VERIFICACIÓN Y CONFIRMACIÓN DE PAGOS", "⏳ 2. PAYMENT VERIFICATION & CONFIRMATION", "⏳ 2. VERIFICAÇÃO E CONFIRMAÇÃO DE PAGAMENTOS"),
        ["BtnConfirmPayment"] = ("✅ Confirmar Pago y Abonar Saldo", "✅ Confirm Payment & Credit Balance", "✅ Confirmar Pagamento e Creditar Saldo"),
        ["WithdrawHeader"] = ("💸 RETIRAR SALDO DISPONIBLE", "💸 WITHDRAW AVAILABLE BALANCE", "💸 SACAR SALDO DISPONÍVEL"),
        ["BtnWithdraw"] = ("Retirar", "Withdraw", "Sacar"),
        ["TransactionsHeader"] = ("📊 HISTORIAL DE MOVIMIENTOS CONFIRMADOS", "📊 CONFIRMED TRANSACTIONS HISTORY", "📊 HISTÓRICO DE TRANSAÇÕES CONFIRMADAS"),

        // Tablero de Partida
        ["PotLabel"] = ("POZO", "POT", "PRÊMIO"),
        ["OpponentLabel"] = ("⚫ CONTRINCANTE", "⚫ OPPONENT", "⚫ OPONENTE"),
        ["ActivePiecesFormat"] = ("{0} {1} activas", "{0} active {1}", "{0} {1} ativas"),
        ["YourTurnCheckers"] = ("Tu turno (Fichas Rojas)", "Your turn (Red Discs)", "Sua vez (Peças Vermelhas)"),
        ["YourTurnChess"] = ("Tu turno (Piezas Blancas ♔)", "Your turn (White Pieces ♔)", "Sua vez (Peças Brancas ♔)"),
        ["WaitingOpponentTurn"] = ("⏳ El contrincante está jugando su turno...", "⏳ Opponent is making a move...", "⏳ O oponente está jogando..."),
        ["HintTapPiece"] = (
            "Toca una de tus fichas/piezas abajo para ver las casillas verdes disponibles.",
            "Tap one of your pieces at the bottom to see valid green squares.",
            "Toque em uma de suas peças abaixo para ver as casas verdes disponíveis."),
        ["BtnDraw"] = ("🤝 Declarar Tablas (Empate)", "🤝 Declare Draw", "🤝 Declarar Empate"),
        ["BtnResign"] = ("🏳️ Rendirse", "🏳️ Resign", "🏳️ Desistir"),
        ["BtnSimulateWin"] = (
            "🏆 Simular Victoria Inmediata (Probar Cobro)",
            "🏆 Simulate Instant Win (Test Payout)",
            "🏆 Simular Vitória Imediata (Testar Prêmio)"),
        ["MoveHistoryHeader"] = ("📋 Historial de Jugadas:", "📋 Move History:", "📋 Histórico de Jogadas:")
    };

    public void SetLanguage(string langCode)
    {
        CurrentLanguage = langCode?.ToLowerInvariant() switch
        {
            "en" => AppLanguage.En,
            "pt" => AppLanguage.Pt,
            _ => AppLanguage.Es
        };
        LanguageChanged?.Invoke();
    }

    public string GetLanguageCode() => CurrentLanguage switch
    {
        AppLanguage.En => "en",
        AppLanguage.Pt => "pt",
        _ => "es"
    };

    public string T(string key)
    {
        if (!Translations.TryGetValue(key, out var item))
            return key;

        return CurrentLanguage switch
        {
            AppLanguage.En => item.En,
            AppLanguage.Pt => item.Pt,
            _ => item.Es
        };
    }

    public string Format(string key, params object[] args)
    {
        return string.Format(T(key), args);
    }
}


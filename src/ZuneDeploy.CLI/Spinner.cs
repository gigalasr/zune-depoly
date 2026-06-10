using System.Diagnostics;
using System.Text;

public static class Spinner {
    private static readonly bool ShouldUseAsciiFrames =
        Environment.OSVersion.Platform == PlatformID.Win32NT
                && Console.OutputEncoding.CodePage != 1200 /* UTF-16 */
                && Console.OutputEncoding.CodePage != 65001 /* UTF-8 */;

    private static readonly string[] _framesUnicode = ["◜", "◠", "◝", "◞", "◡", "◟"];
    private static readonly string[] _framesAscii = ["-", "\\", "|", "/"];
    private static readonly string[] _frames = ShouldUseAsciiFrames ? _framesAscii : _framesUnicode;
    private static readonly string _successSymbol = ShouldUseAsciiFrames ? "[OK]" : "✓";
    private static readonly string _failureSymbol = ShouldUseAsciiFrames ? "[FAIL]" : "🞬";

    private static readonly object _lock = new();
    private static int _spinnerRow = Console.CursorTop;
    private static int _frame = 0;
    private static int _lastLenght = 0;
    private static string _label = "";

    private static readonly TextWriter _originalOut = Console.Out;
    private static readonly TextWriter _newOut = new Writer(Console.Out.Encoding);

    private static Task? _spinnerTask = null;
    private static CancellationTokenSource _cts = new();

    public static void SpinFor(string label, Action work, string? finalLabel = null) {
        SpinFor<object?>(label, () => { work(); return null; }, finalLabel == null ? null : _ => finalLabel);
    }

    public static T SpinFor<T>(string label, Func<T> work, Func<T, string>? finalLabel = null) {
        Start(label);
        T result = work();
        Trace.Assert(label.Contains("ing"));
        if (finalLabel == null) {
            Stop(label.Replace("ing", "ed"));
        } else {
            Stop(finalLabel(result));
        }
        return result;
    }

    public static void Start(string label) {
        if (_spinnerTask != null) {
            SetLabel(label);
            return;
        }

        _label = label;
        _spinnerRow = Console.CursorTop;
        _cts = new CancellationTokenSource();
        Console.SetOut(_newOut);
        _spinnerTask = Task.Run(async () => { await Spin(_cts.Token); });
    }

    public static void Stop(string finalLabel, bool faulted = false) {
        if (_spinnerTask == null) {
            Console.WriteLine(finalLabel);
            return;
        }

        _cts.Cancel();
        _spinnerTask.Wait();
        lock (_lock) {
            _spinnerTask = null;
            Console.SetOut(_originalOut);
            Console.SetCursorPosition(0, _spinnerRow);
            var symbol = faulted ? _failureSymbol : _successSymbol;
            Console.WriteLine($"{symbol} {finalLabel.PadRight(_lastLenght)}");
        }
    }

    public static void SetLabel(string label) {
        lock (_lock) { _label = label; }
    }

    private static void Log(string line) {
        lock (_lock) {
            Console.SetCursorPosition(0, _spinnerRow);
            _originalOut.WriteLine($"{line.PadRight(_lastLenght)}");
            _spinnerRow++;
            DrawSpinner();
        }
    }

    private static async Task Spin(CancellationToken token) {
        while (!token.IsCancellationRequested) {
            lock (_lock) { DrawSpinner(); }
            await Task.Delay(80, token).ContinueWith(_ => { });
        }
    }

    private static void DrawSpinner() {
        Console.SetCursorPosition(0, _spinnerRow);
        var line = $"{_frames[_frame]} {_label}".PadRight(_lastLenght);
        _originalOut.Write(line);
        _lastLenght = line.Length;
        _frame = (_frame + 1) % _frames.Length;
    }

    internal class Writer(Encoding encoding) : TextWriter {
        public override Encoding Encoding => _encoding;
        private readonly StringBuilder _buffer = new();
        private readonly Encoding _encoding = encoding;

        public override void Write(char value) {
            _buffer.Append(value);
            if (value == '\n') {
                Flush();
            }
        }

        public override void WriteLine(string? line) {
            if (line != null) { _buffer.Append(line); }
            Flush();
        }

        public override void Flush() {
            Log(_buffer.ToString());
            _buffer.Clear();
        }
    }
}

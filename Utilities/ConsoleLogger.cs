using System;
using Commons.VersionBumper.Interfaces;

namespace Commons.VersionBumper.Utilities
{
	public class ConsoleLogger : ILogger
	{
		public ConsoleLogger(bool debug, bool info = true, bool warn = true)
		{
			IsDebugEnabled = debug;
			IsInfoEnabled = info;
			IsWarnEnabled = warn;
			_indentSpacer = string.Empty;
			_indentLevel = 0;
		}

		public IDisposable Block { get { return new Indenter(this, false); } }

		public bool IsDebugEnabled { get; private set; }

		public bool IsInfoEnabled { get; private set; }

		public bool IsWarnEnabled { get; private set; }

		public IDisposable QuietBlock { get { return new Indenter(this, true); } }

		public void Debug(Exception exception, string message = null)
		{
			LogDebug(() => FormatException(exception, message));
		}

		public void Debug(string format, params object[] args)
		{
			LogDebug(() => Format(format, args));
		}

		public void Error(Exception exception, string message = null)
		{
			LogError(() => FormatException(exception, message));
		}

		public void Error(string format, params object[] args)
		{
			LogError(() => Format(format, args));
		}

		public void ErrorDetail(string format, params object[] args)
		{
			Log(ConsoleColor.Yellow, "", () => Format(format, args));
		}

		public void Info(string format, params object[] args)
		{
			if (IsInfoEnabled)
				Log(ConsoleColor.White, string.Empty, () => Format(format, args));
		}

		public void Warn(Exception exception, string message = null)
		{
			LogWarn(() => FormatException(exception, message));
		}

		public void Warn(string format, params object[] args)
		{
			LogWarn(() => Format(format, args));
		}

		private int _indentLevel;

		private string _indentSpacer;

		private static string Format(string format, params object[] args)
		{
			return args.Length == 0 ? format : string.Format(format, args);
		}

		private static string FormatException(Exception exception, string message)
		{
			return string.IsNullOrWhiteSpace(message) ? exception.ToString() : (message + Environment.NewLine + exception);
		}

		private void Log(ConsoleColor foregroundColor, string prefix, Func<string> emit)
		{
			try {
				string message = emit();
				if (string.IsNullOrWhiteSpace(message))
					return;
				Console.ForegroundColor = foregroundColor;
				Console.Write(_indentSpacer);
				Console.Write(prefix);
				Console.WriteLine(message);
			} finally {
				Console.ResetColor();
			}
		}

		private void LogDebug(Func<string> emit)
		{
			if (IsDebugEnabled)
				Log(ConsoleColor.Gray, "DEBUG: ", emit);
		}

		private void LogError(Func<string> emit)
		{
			Log(ConsoleColor.Red, "ERRO: ", emit);
		}

		private void LogWarn(Func<string> emit)
		{
			if (IsDebugEnabled)
				Log(ConsoleColor.Yellow, "WARN: ", emit);
		}

		private class Indenter : IDisposable
		{
			public Indenter(ConsoleLogger parent, bool quiet)
			{
				_parent = parent;
				_debug = _parent.IsDebugEnabled;
				_info = _parent.IsInfoEnabled;
				_warn = _parent.IsWarnEnabled;
				if (quiet)
					_parent.IsDebugEnabled = _parent.IsInfoEnabled = _parent.IsWarnEnabled = false;
				_parent._indentLevel++;
				_parent._indentSpacer = new String(' ', _parent._indentLevel * 4);
			}

			public void Dispose()
			{
				if (_parent._indentLevel > 0) {
					_parent._indentLevel--;
					_parent._indentSpacer = new String(' ', _parent._indentLevel * 4);
				}
				_parent.IsDebugEnabled = _debug;
				_parent.IsInfoEnabled = _info;
				_parent.IsWarnEnabled = _warn;
			}

			private bool _debug;
			private bool _info;
			private ConsoleLogger _parent;
			private bool _warn;
		}
	}
}
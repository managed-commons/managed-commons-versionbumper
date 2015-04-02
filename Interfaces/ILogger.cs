using System;

namespace Commons.VersionBumper.Interfaces
{
	public interface ILogger
	{
		IDisposable Block { get; }

		bool IsDebugEnabled { get; }

		bool IsInfoEnabled { get; }

		bool IsWarnEnabled { get; }

		IDisposable QuietBlock { get; }

		void Debug(Exception exception, string message = null);

		void Debug(string format, params object[] args);

		void Error(Exception exception, string message = null);

		void Error(string format, params object[] args);

		void ErrorDetail(string format, params object[] args);

		void Info(string format, params object[] args);

		void Warn(Exception exception, string message = null);

		void Warn(string format, params object[] args);
	}
}
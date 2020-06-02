using System;
using MaO2.Common.Enums;
using VRage.Game;

namespace MaO2.Common.BaseClasses
{
	public abstract class BaseClosableLoggingClass
	{
		public event Action<string, string, LogType, bool, int, string> OnWriteToLog;

		protected abstract string Id { get; }
		
		protected void WriteToLog(string caller, string message, LogType type, bool showOnHud = false, int duration = Settings.DefaultLocalMessageDisplayTime, string color = MyFontEnum.Green)
		{
			OnWriteToLog?.Invoke($"{Id}: {caller}", message, type, showOnHud, duration, color);
		}

		private bool _isClosed;

		public virtual void Close()
		{
			if (_isClosed) return;
			_isClosed = true;
		}
		
	}
}
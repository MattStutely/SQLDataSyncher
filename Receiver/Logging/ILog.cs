using System;

namespace Logger
{
    public interface ILog<T>
    {
        #region Debug
        void Debug(object message);
        void Debug(object message, Exception exception);
        #endregion

        #region Info
        void Info(object message);
        void Info(object message, Exception exception);
        #endregion

        #region Warn
        void Warn(object message);
        void Warn(object message, Exception exception);
        #endregion

        #region Error
        void Error(object message);
        void Error(object message, Exception exception);
        #endregion

        #region Fatal
        void Fatal(object message);
        void Fatal(object message, Exception exception);
        #endregion
    }
}

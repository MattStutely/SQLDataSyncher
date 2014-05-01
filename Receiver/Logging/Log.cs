using log4net;
using log4net.Repository.Hierarchy;
using System;

namespace Logger
{

    public class Log<T> : ILog<T>
    { 
        private readonly ILog _log;

        public Log()   
        {
            var root = ((Hierarchy)LogManager.GetRepository()).Root;
            if (!root.Repository.Configured)
                LogSetup.Initialize();

            _log = LogManager.GetLogger(typeof(T));
        }

        #region Debug
        public void Debug(object message)
        {    
            _log.Debug(message);
        }         
        
        public void Debug(object message, Exception exception)
        {            
            _log.Debug(message, exception);
        }              
        #endregion         
        
        #region Info         
        public void Info(object message)        
        {           
            _log.Info(message);
        }        
        
        public void Info(object message, Exception exception)
        {            
            _log.Info(message, exception);
        }             
        #endregion         
        
        #region Warn         
        public void Warn(object message) 
        {           
            _log.Warn(message);
        }         
        
        public void Warn(object message, Exception exception)
        {           
            _log.Warn(message, exception);
        }              
        #endregion  
   
        #region Error      
        public void Error(object message)   
        {            
            _log.Error(message);  
        }        
        public void Error(object message, Exception exception)  
        {          
            _log.Error(message, exception);   
        }    
        #endregion  
 
        #region Fatal   
        public void Fatal(object message)    
        {        
            _log.Fatal(message);   
        }       
        public void Fatal(object message, Exception exception)  
        {      
            _log.Fatal(message, exception);   
        }          
        #endregion  
 
        #region Properties    
        public bool IsDebugEnabled   
        {       
            get { return _log.IsDebugEnabled; }  
        }      
        public bool IsInfoEnabled  
        {       
            get { return _log.IsInfoEnabled; }  
        }     
        public bool IsWarnEnabled   
        {          
            get { return _log.IsWarnEnabled; } 
        }         
        public bool IsErrorEnabled 
        {     
            get { return _log.IsErrorEnabled; }  
        }        
        public bool IsFatalEnabled     
        {    
            get { return _log.IsFatalEnabled; }    
        }    
        #endregion
    }
}

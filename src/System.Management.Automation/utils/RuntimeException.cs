// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation.Language;
using System.Runtime.Serialization;

namespace System.Management.Automation
{
    /// <summary>
    /// RuntimeException is the base class for exceptions likely to occur
    /// while a PowerShell command is running.
    /// </summary>
    /// <remarks>
    /// PowerShell scripts can trap RuntimeException using the
    /// "trap (exceptionclass) {handler}" script construct.
    ///
    /// Instances of this exception class are usually generated by the
    /// PowerShell Engine.  It is unusual for code outside the PowerShell Engine
    /// to create an instance of this class.
    /// </remarks>
    [Serializable]
    public class RuntimeException
            : SystemException, IContainsErrorRecord
    {
        #region ctor
        /// <summary>
        /// Initializes a new instance of the RuntimeException class.
        /// </summary>
        /// <returns>Constructed object.</returns>
        public RuntimeException()
            : base()
        {
        }

        #region Serialization
        /// <summary>
        /// Initializes a new instance of the RuntimeException class
        /// using data serialized via
        /// <see cref="ISerializable"/>
        /// </summary>
        /// <param name="info">Serialization information.</param>
        /// <param name="context">Streaming context.</param>
        /// <returns>Constructed object.</returns>
        protected RuntimeException(SerializationInfo info,
                           StreamingContext context)
                : base(info, context)
        {
            _errorId = info.GetString("ErrorId");
            _errorCategory = (ErrorCategory)info.GetInt32("ErrorCategory");
        }

        /// <summary>
        /// Serializer for <see cref="ISerializable"/>
        /// </summary>
        /// <param name="info">Serialization information.</param>
        /// <param name="context">Streaming context.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new PSArgumentNullException(nameof(info));
            }

            base.GetObjectData(info, context);
            info.AddValue("ErrorId", _errorId);
            info.AddValue("ErrorCategory", (int)_errorCategory);
        }
        #endregion Serialization

        /// <summary>
        /// Initializes a new instance of the RuntimeException class.
        /// </summary>
        /// <param name="message"></param>
        /// <returns>Constructed object.</returns>
        public RuntimeException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the RuntimeException class.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        /// <returns>Constructed object.</returns>
        public RuntimeException(string message,
                                Exception innerException)
                : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the RuntimeException class
        /// starting with an already populated error record.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        /// <param name="errorRecord"></param>
        /// <returns>Constructed object.</returns>
        public RuntimeException(string message,
            Exception innerException,
            ErrorRecord errorRecord)
                : base(message, innerException)
        {
            _errorRecord = errorRecord;
        }

        internal RuntimeException(ErrorCategory errorCategory,
            InvocationInfo invocationInfo,
            IScriptExtent errorPosition,
            string errorIdAndResourceId,
            string message,
            Exception innerException)
            : base(message, innerException)
        {
            SetErrorCategory(errorCategory);
            SetErrorId(errorIdAndResourceId);

            if ((errorPosition == null) && (invocationInfo != null))
            {
                errorPosition = invocationInfo.ScriptPosition;
            }

            if (invocationInfo == null) return;
            _errorRecord = new ErrorRecord(
              new ParentContainsErrorRecordException(this),
                _errorId,
                _errorCategory,
                _targetObject);
            _errorRecord.SetInvocationInfo(new InvocationInfo(invocationInfo.MyCommand, errorPosition));
        }

        #endregion ctor

        #region ErrorRecord
        // If RuntimeException subclasses need to do more than change
        // the ErrorId, ErrorCategory and TargetObject, they can access
        // the ErrorRecord property and make changes directly.  However,
        // not that calling SetErrorId, SetErrorCategory or SetTargetObject
        // will clean the cached ErrorRecord and erase any other changes,
        // so the ErrorId etc. should be set first.
        /// <summary>
        /// Additional information about the error.
        /// </summary>
        /// <value></value>
        /// <remarks>
        /// Note that ErrorRecord.Exception is
        /// <see cref="System.Management.Automation.ParentContainsErrorRecordException"/>.
        /// </remarks>
        public virtual ErrorRecord ErrorRecord
        {
            get
            {
                _errorRecord ??= new ErrorRecord(
                    new ParentContainsErrorRecordException(this),
                    _errorId,
                    _errorCategory,
                    _targetObject);

                return _errorRecord;
            }
        }

        private ErrorRecord _errorRecord;
        private string _errorId = "RuntimeException";
        private ErrorCategory _errorCategory = ErrorCategory.NotSpecified;
        private object _targetObject = null;

        /// <summary>
        /// Subclasses can use this method to set the ErrorId.
        /// Note that this will clear the cached ErrorRecord, so be sure
        /// to change this before writing to ErrorRecord.ErrorDetails
        /// or the like.
        /// </summary>
        /// <param name="errorId">Per ErrorRecord constructors.</param>
        internal void SetErrorId(string errorId)
        {
            if (_errorId != errorId)
            {
                _errorId = errorId;
                _errorRecord = null;
            }
        }

        /// <summary>
        /// Subclasses can use this method to set the ErrorCategory.
        /// Note that this will clear the cached ErrorRecord, so be sure
        /// to change this before writing to ErrorRecord.ErrorDetails
        /// or the like.
        /// </summary>
        /// <param name="errorCategory">
        /// per ErrorRecord.CategoryInfo.Category
        /// </param>
        internal void SetErrorCategory(ErrorCategory errorCategory)
        {
            if (_errorCategory != errorCategory)
            {
                _errorCategory = errorCategory;
                _errorRecord = null;
            }
        }

        /// <summary>
        /// Subclasses can use this method to set or update the TargetObject.
        /// This convenience function doesn't clobber the error record if it
        /// already exists...
        /// </summary>
        /// <param name="targetObject">
        /// per ErrorRecord.TargetObject
        /// </param>
        internal void SetTargetObject(object targetObject)
        {
            _targetObject = targetObject;
            _errorRecord?.SetTargetObject(targetObject);
        }
        #endregion ErrorRecord

        #region Internal
        internal static string RetrieveMessage(ErrorRecord errorRecord)
        {
            if (errorRecord == null)
                return string.Empty;
            if (errorRecord.ErrorDetails != null &&
                !string.IsNullOrEmpty(errorRecord.ErrorDetails.Message))
            {
                return errorRecord.ErrorDetails.Message;
            }

            if (errorRecord.Exception == null)
                return string.Empty;
            return errorRecord.Exception.Message;
        }

        internal static string RetrieveMessage(Exception e)
        {
            if (e == null)
                return string.Empty;

            if (!(e is IContainsErrorRecord icer))
                return e.Message;
            ErrorRecord er = icer.ErrorRecord;
            if (er == null)
                return e.Message;
            ErrorDetails ed = er.ErrorDetails;
            if (ed == null)
                return e.Message;
            string detailsMessage = ed.Message;
            return (string.IsNullOrEmpty(detailsMessage)) ? e.Message : detailsMessage;
        }

        internal static Exception RetrieveException(ErrorRecord errorRecord)
        {
            if (errorRecord == null)
                return null;
            return errorRecord.Exception;
        }

        /// <summary>
        /// </summary>
        public bool WasThrownFromThrowStatement
        {
            get
            {
                return _thrownByThrowStatement;
            }

            set
            {
                _thrownByThrowStatement = value;
                if (_errorRecord != null)
                {
                    RuntimeException exception = _errorRecord.Exception as RuntimeException;
                    if (exception != null)
                    {
                        exception.WasThrownFromThrowStatement = value;
                    }
                }
            }
        }

        private bool _thrownByThrowStatement;

        internal bool WasRethrown { get; set; }

        /// <summary>
        /// Fix for BUG: Windows Out Of Band Releases: 906263 and 906264
        /// The interpreter prompt CommandBaseStrings:InquireHalt
        /// should be suppressed when this flag is set.  This will be set
        /// when this prompt has already occurred and Break was chosen,
        /// or for ActionPreferenceStopException in all cases.
        /// </summary>
        internal bool SuppressPromptInInterpreter
        {
            get { return _suppressPromptInInterpreter; }

            set { _suppressPromptInInterpreter = value; }
        }

        private bool _suppressPromptInInterpreter;

        #endregion Internal

        private Token _errorToken;

        internal Token ErrorToken
        {
            get
            {
                return _errorToken;
            }

            set
            {
                _errorToken = value;
            }
        }
    }
}

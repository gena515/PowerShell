// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.Serialization;
using System.Security.Permissions;

namespace System.Management.Automation
{
    /// <summary>
    /// This is a wrapper for exception class
    /// <see cref="System.InvalidOperationException"/>
    /// which provides additional information via
    /// <see cref="System.Management.Automation.IContainsErrorRecord"/>.
    /// </summary>
    /// <remarks>
    /// Instances of this exception class are usually generated by the
    /// Monad Engine.  It is unusual for code outside the Monad Engine
    /// to create an instance of this class.
    /// </remarks>
    [Serializable]
    public class PSInvalidOperationException
            : InvalidOperationException, IContainsErrorRecord
    {
        #region ctor
        /// <summary>
        /// Initializes a new instance of the PSInvalidOperationException class.
        /// </summary>
        /// <returns> constructed object </returns>
        public PSInvalidOperationException()
            : base()
        {
        }

        #region Serialization
        /// <summary>
        /// Initializes a new instance of the PSInvalidOperationException class
        /// using data serialized via
        /// <see cref="System.Runtime.Serialization.ISerializable"/>
        /// </summary>
        /// <param name="info">Serialization information.</param>
        /// <param name="context">Streaming context.</param>
        /// <returns> constructed object </returns>
        protected PSInvalidOperationException(SerializationInfo info,
                           StreamingContext context)
                : base(info, context)
        {
            _errorId = info.GetString("ErrorId");
        }

        /// <summary>
        /// Serializer for <see cref="System.Runtime.Serialization.ISerializable"/>
        /// </summary>
        /// <param name="info">Serialization information.</param>
        /// <param name="context">Streaming context.</param>
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new PSArgumentNullException("info");
            }

            base.GetObjectData(info, context);
            info.AddValue("ErrorId", _errorId);
        }
        #endregion Serialization

        /// <summary>
        /// Initializes a new instance of the PSInvalidOperationException class.
        /// </summary>
        /// <param name="message"> .</param>
        /// <returns> constructed object </returns>
        public PSInvalidOperationException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the PSInvalidOperationException class.
        /// </summary>
        /// <param name="message"> .</param>
        /// <param name="innerException"> .</param>
        /// <returns> constructed object </returns>
        public PSInvalidOperationException(string message,
                                            Exception innerException)
                : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the PSInvalidOperationException class.
        /// </summary>
        /// <param name="message"> .</param>
        /// <param name="innerException"> .</param>
        /// <param name="errorId"> .</param>
        /// <param name="errorCategory"> .</param>
        /// <param name="target"> .</param>
        /// <returns> constructed object </returns>
        internal PSInvalidOperationException(string message, Exception innerException, string errorId, ErrorCategory errorCategory, object target)
            : base(message, innerException)
        {
            _errorId = errorId;
            _errorCategory = errorCategory;
            _target = target;
        }
        #endregion ctor

        /// <summary>
        /// Additional information about the error
        /// </summary>
        /// <value></value>
        /// <remarks>
        /// Note that ErrorRecord.Exception is
        /// <see cref="System.Management.Automation.ParentContainsErrorRecordException"/>.
        /// </remarks>
        public ErrorRecord ErrorRecord
        {
            get
            {
                if (_errorRecord == null)
                {
                    _errorRecord = new ErrorRecord(
                        new ParentContainsErrorRecordException(this),
                        _errorId,
                        _errorCategory,
                        _target);
                }

                return _errorRecord;
            }
        }

        private ErrorRecord _errorRecord;
        private string _errorId = "InvalidOperation";
        internal void SetErrorId(string errorId)
        {
            _errorId = errorId;
        }

        private ErrorCategory _errorCategory = ErrorCategory.InvalidOperation;
        private object _target = null;
    }
}


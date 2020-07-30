// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation;

using Microsoft.PowerShell.Commands.Internal.Format;

namespace Microsoft.PowerShell.Commands
{
    internal class ExpressionColumnInfo : ColumnInfo
    {
        private PSPropertyExpression _expression;

        internal ExpressionColumnInfo(string staleObjectPropertyName, string displayName, PSPropertyExpression expression)
            : base(staleObjectPropertyName, displayName)
        {
            _expression = expression;
        }

        internal override object GetValue(PSObject liveObject)
        {
            List<PSPropertyExpressionResult> resList = _expression.GetValues(liveObject);

            if (resList.Count == 0)
            {
                return null;
            }

            // Only first element is used.
            PSPropertyExpressionResult result = resList[0];
            if (result.Exception is not null)
            {
                return null;
            }

            object objectResult = result.Result;
            return objectResult is null ? string.Empty : ColumnInfo.LimitString(objectResult.ToString());
        }
    }
}

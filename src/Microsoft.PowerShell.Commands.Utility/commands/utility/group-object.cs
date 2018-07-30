// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Text;

namespace Microsoft.PowerShell.Commands
{
    /// <summary>
    /// PSTuple is a helper class used to create Tuple from an input array.
    /// </summary>
    internal static class PSTuple
    {
        /// <summary>
        /// ArrayToTuple is a helper method used to create a tuple for the supplied input array.
        /// </summary>
        /// <param name="inputObjects">Input objects used to create a tuple.</param>
        /// <returns>Tuple object.</returns>
        internal static object ArrayToTuple<T>(IList<T> inputObjects)
        {
            Diagnostics.Assert(inputObjects != null, "inputObjects is null");
            Diagnostics.Assert(inputObjects.Count > 0, "inputObjects is empty");

            return ArrayToTuple(inputObjects, 0);
        }

        /// <summary>
        /// ArrayToTuple is a helper method used to create a tuple for the supplied input array.
        /// </summary>
        /// <param name="inputObjects">Input objects used to create a tuple</param>
        /// <param name="startIndex">Start index of the array from which the objects have to considered for the tuple creation.</param>
        /// <returns>Tuple object.</returns>
        internal static object ArrayToTuple<T>(IList<T> inputObjects, int startIndex)
        {
            Diagnostics.Assert(inputObjects != null, "inputObjects is null");
            Diagnostics.Assert(inputObjects.Count > 0, "inputObjects is empty");

            switch (inputObjects.Count - startIndex)
            {
                case 0:
                    return null;
                case 1:
                    return Tuple.Create(inputObjects[startIndex]);
                case 2:
                    return Tuple.Create(inputObjects[startIndex], inputObjects[startIndex + 1]);
                case 3:
                    return Tuple.Create(inputObjects[startIndex], inputObjects[startIndex + 1], inputObjects[startIndex + 2]);
                case 4:
                    return Tuple.Create(inputObjects[startIndex], inputObjects[startIndex + 1], inputObjects[startIndex + 2], inputObjects[startIndex + 3]);
                case 5:
                    return Tuple.Create(inputObjects[startIndex], inputObjects[startIndex + 1], inputObjects[startIndex + 2], inputObjects[startIndex + 3], inputObjects[startIndex + 4]);
                case 6:
                    return Tuple.Create(inputObjects[startIndex], inputObjects[startIndex + 1], inputObjects[startIndex + 2], inputObjects[startIndex + 3], inputObjects[startIndex + 4],
                        inputObjects[startIndex + 5]);
                case 7:
                    return Tuple.Create(inputObjects[startIndex], inputObjects[startIndex + 1], inputObjects[startIndex + 2], inputObjects[startIndex + 3], inputObjects[startIndex + 4],
                        inputObjects[startIndex + 5], inputObjects[startIndex + 6]);
                case 8:
                    return Tuple.Create(inputObjects[startIndex], inputObjects[startIndex + 1], inputObjects[startIndex + 2], inputObjects[startIndex + 3], inputObjects[startIndex + 4],
                        inputObjects[startIndex + 5], inputObjects[startIndex + 6], inputObjects[startIndex + 7]);
                default:
                    return Tuple.Create(inputObjects[startIndex], inputObjects[startIndex + 1], inputObjects[startIndex + 2], inputObjects[startIndex + 3], inputObjects[startIndex + 4],
                        inputObjects[startIndex + 5], inputObjects[startIndex + 6], ArrayToTuple(inputObjects, startIndex + 7));
            }
        }
    }

    /// <summary>
    /// Emitted by Group-Object when the NoElement option is true.
    /// </summary>
    public sealed class GroupInfoNoElement : GroupInfo
    {
        internal GroupInfoNoElement(OrderByPropertyEntry groupValue)
            : base(groupValue)
        {
        }

        internal override void Add(PSObject groupValue)
        {
            Count++;
        }
    }

    /// <summary>
    /// Emitted by Group-Object.
    /// </summary>
    [DebuggerDisplay("{Name} ({Count})")]
    public class GroupInfo
    {
        internal GroupInfo(OrderByPropertyEntry groupValue)
        {
            Group = new Collection<PSObject>();
            this.Add(groupValue.inputObject);
            GroupValue = groupValue;
            Name = BuildName(groupValue.orderValues);
        }

        internal virtual void Add(PSObject groupValue)
        {
            Group.Add(groupValue);
            Count++;
        }

        private static string BuildName(List<ObjectCommandPropertyValue> propValues)
        {
            StringBuilder sb = new StringBuilder();
            foreach (ObjectCommandPropertyValue propValue in propValues)
            {
                var propValuePropertyValue = propValue?.PropertyValue;
                if (propValuePropertyValue != null)
                {
                    if (propValuePropertyValue is ICollection propertyValueItems)
                    {
                        sb.Append("{");
                        var length = sb.Length;

                        foreach (object item in propertyValueItems)
                        {
                            sb.Append(string.Format(CultureInfo.InvariantCulture, "{0}, ", item.ToString()));
                        }

                        sb = sb.Length > length ? sb.Remove(sb.Length - 2, 2) : sb;
                        sb.Append("}, ");
                    }
                    else
                    {
                        sb.Append(string.Format(CultureInfo.InvariantCulture, "{0}, ", propValuePropertyValue.ToString()));
                    }
                }
            }

            return sb.Length >= 2 ? sb.Remove(sb.Length - 2, 2).ToString() : string.Empty;
        }

        /// <summary>
        /// Values of the group.
        /// </summary>
        public ArrayList Values
        {
            get
            {
                ArrayList values = new ArrayList();
                foreach (ObjectCommandPropertyValue propValue in GroupValue.orderValues)
                {
                    values.Add(propValue.PropertyValue);
                }

                return values;
            }
        }

        /// <summary>
        /// Number of objects in the group.
        /// </summary>
        public int Count { get; internal set; }

        /// <summary>
        /// The list of objects in this group.
        /// </summary>
        public Collection<PSObject> Group { get; }

        /// <summary>
        /// The name of the group.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The OrderByPropertyEntry used to build this group object.
        /// </summary>
        internal OrderByPropertyEntry GroupValue { get; }
    }

    /// <summary>
    /// Group-Object implementation.
    /// </summary>
    [Cmdlet(VerbsData.Group, "Object", HelpUri = "https://go.microsoft.com/fwlink/?LinkID=113338", RemotingCapability = RemotingCapability.None)]
    [OutputType(typeof(Hashtable), typeof(GroupInfo))]
    public class GroupObjectCommand : ObjectBase
    {
        #region tracer

        /// <summary>
        /// An instance of the PSTraceSource class used for trace output.
        /// </summary>
        [TraceSourceAttribute(
            "GroupObjectCommand",
            "Class that has group base implementation")]
        private static PSTraceSource s_tracer =
            PSTraceSource.GetTracer("GroupObjectCommand",
                "Class that has group base implementation");

        #endregion tracer

        #region Command Line Switches

        /// <summary>
        /// Flatten the groups.
        /// </summary>
        /// <value></value>
        [Parameter]
        public SwitchParameter NoElement { get; set; }

        /// <summary>
        /// the AsHashTable parameter.
        /// </summary>
        /// <value></value>
        [Parameter(ParameterSetName = "HashTable")]
        [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "HashTable")]
        [Alias("AHT")]
        public SwitchParameter AsHashTable { get; set; }

        /// <summary>
        ///
        /// </summary>
        /// <value></value>
        [Parameter(ParameterSetName = "HashTable")]
        public SwitchParameter AsString { get; set; }

        private readonly List<GroupInfo> _groups = new List<GroupInfo>();
        private readonly OrderByProperty _orderByProperty = new OrderByProperty();
        private readonly Dictionary<object, GroupInfo> _tupleToGroupInfoMappingDictionary = new Dictionary<object, GroupInfo>();
        private readonly List<OrderByPropertyEntry> _orderedEntries = new List<OrderByPropertyEntry>();
        private OrderByPropertyComparer _orderByPropertyComparer;
        private bool _hasProcessedFirstInputObject;

        #endregion

        #region utils

        /// <summary>
        /// Utility function called by Group-Object to create Groups.
        /// </summary>
        /// <param name="currentObjectEntry">Input object that needs to be grouped.</param>
        /// <param name="noElement">true if we are not accumulating objects</param>
        /// <param name="groups">List containing Groups.</param>
        /// <param name="groupInfoDictionary">Dictionary used to keep track of the groups with hash of the property values being the key.</param>
        /// <param name="orderByPropertyComparer">The Comparer to be used while comparing to check if new group has to be created.</param>
        internal static void DoGrouping(OrderByPropertyEntry currentObjectEntry, bool noElement, List<GroupInfo> groups, Dictionary<object, GroupInfo> groupInfoDictionary,
            OrderByPropertyComparer orderByPropertyComparer)
        {
            var currentObjectorderValues = currentObjectEntry.orderValues;
            if (currentObjectorderValues != null && currentObjectorderValues.Count > 0)
            {
                object currentTupleObject = PSTuple.ArrayToTuple(currentObjectorderValues);

                if (groupInfoDictionary.TryGetValue(currentTupleObject, out var currentGroupInfo))
                {
                    //add this inputObject to an existing group
                    currentGroupInfo?.Add(currentObjectEntry.inputObject);
                }
                else
                {
                    bool isCurrentItemGrouped = false;

                    if (groups.Count > 0)
                    {
                        var lastGroup = groups[groups.Count - 1];
                        // Check if the current input object can be converted to one of the already known types
                        // by looking up in the type to GroupInfo mapping.
                        if (orderByPropertyComparer.Compare(lastGroup.GroupValue, currentObjectEntry) == 0)
                        {
                            lastGroup.Add(currentObjectEntry.inputObject);
                            isCurrentItemGrouped = true;
                        }
                    }

                    if (!isCurrentItemGrouped)
                    {
                        // create a new group
                        s_tracer.WriteLine("Create a new group: {0}", currentObjectorderValues);
                        GroupInfo newObjGrp = noElement
                            ? new GroupInfoNoElement(currentObjectEntry)
                            : new GroupInfo(currentObjectEntry);
                        groups.Add(newObjGrp);

                        groupInfoDictionary.Add(currentTupleObject, newObjGrp);
                    }
                }
            }
        }

        private void WriteNonTerminatingError(Exception exception, string resourceIdAndErrorId,
            ErrorCategory category)
        {
            Exception ex = new Exception(StringUtil.Format(resourceIdAndErrorId), exception);
            WriteError(new ErrorRecord(ex, resourceIdAndErrorId, category, null));
        }

        #endregion utils

        /// <summary>
        /// Process every input object to group them.
        /// </summary>
        protected override void ProcessRecord()
        {
            if (InputObject != null && InputObject != AutomationNull.Value)
            {
                OrderByPropertyEntry currentEntry;

                if (!_hasProcessedFirstInputObject)
                {
                    if (Property == null)
                    {
                        Property = OrderByProperty.GetDefaultKeyPropertySet(InputObject);
                    }

                    _orderByProperty.ProcessExpressionParameter(this, Property);

                    currentEntry = _orderByProperty.CreateOrderByPropertyEntry(this, InputObject, CaseSensitive, _cultureInfo);
                    bool[] ascending = new bool[currentEntry.orderValues.Count];
                    for (int index = 0; index < currentEntry.orderValues.Count; index++)
                    {
                        ascending[index] = true;
                    }

                    _orderByPropertyComparer = new OrderByPropertyComparer(ascending, _cultureInfo, CaseSensitive);

                    _hasProcessedFirstInputObject = true;
                }
                else
                {
                    currentEntry = _orderByProperty.CreateOrderByPropertyEntry(this, InputObject, CaseSensitive, _cultureInfo);
                }

                _orderedEntries.Add(currentEntry);
            }
        }

        /// <summary>
        ///
        /// </summary>
        protected override void EndProcessing()
        {
            // using OrderBy to get stable sort.
            foreach (var entry in _orderedEntries.OrderBy(e => e, _orderByPropertyComparer))
            {
                DoGrouping(entry, NoElement, _groups, _tupleToGroupInfoMappingDictionary, _orderByPropertyComparer);
            }

            s_tracer.WriteLine(_groups.Count);
            if (_groups.Count > 0)
            {
                if (AsHashTable)
                {
                    Hashtable table = CollectionsUtil.CreateCaseInsensitiveHashtable();
                    try
                    {
                        foreach (GroupInfo grp in _groups)
                        {
                            if (AsString)
                            {
                                table.Add(grp.Name, grp.Group);
                            }
                            else
                            {
                                if (grp.Values.Count == 1)
                                {
                                    table.Add(PSObject.Base(grp.Values[0]), grp.Group);
                                }
                                else
                                {
                                    ArgumentException ex = new ArgumentException(UtilityCommonStrings.GroupObjectSingleProperty);
                                    ErrorRecord er = new ErrorRecord(ex, "ArgumentException", ErrorCategory.InvalidArgument, Property);
                                    ThrowTerminatingError(er);
                                }
                            }
                        }
                    }
                    catch (ArgumentException e)
                    {
                        WriteNonTerminatingError(e, UtilityCommonStrings.InvalidOperation, ErrorCategory.InvalidArgument);
                        return;
                    }

                    WriteObject(table);
                }
                else
                {
                    if (AsString)
                    {
                        ArgumentException ex = new ArgumentException(UtilityCommonStrings.GroupObjectWithHashTable);
                        ErrorRecord er = new ErrorRecord(ex, "ArgumentException", ErrorCategory.InvalidArgument, AsString);
                        ThrowTerminatingError(er);
                    }
                    else
                    {
                        WriteObject(_groups, true);
                    }
                }
            }
        }
    }
}
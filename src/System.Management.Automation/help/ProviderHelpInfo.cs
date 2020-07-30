// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Xml;

namespace System.Management.Automation
{
    /// <summary>
    /// Class ProviderHelpInfo keeps track of help information to be returned by
    /// command help provider.
    /// </summary>
    internal class ProviderHelpInfo : HelpInfo
    {
        /// <summary>
        /// Constructor for HelpProvider.
        /// </summary>
        private ProviderHelpInfo(XmlNode xmlNode)
        {
            MamlNode mamlNode = new MamlNode(xmlNode);
            _fullHelpObject = mamlNode.PSObject;
            this.Errors = mamlNode.Errors;

            _fullHelpObject.TypeNames.Clear();
            _fullHelpObject.TypeNames.Add("ProviderHelpInfo");
            _fullHelpObject.TypeNames.Add("HelpInfo");
        }

        #region Basic Help Properties / Methods

        /// <summary>
        /// Name of the provider for which this provider help info is for.
        /// </summary>
        /// <value>Name of the provider</value>
        internal override string Name
        {
            get
            {
                if (_fullHelpObject is null)
                    return string.Empty;

                if (_fullHelpObject.Properties["Name"] is null)
                    return string.Empty;

                if (_fullHelpObject.Properties["Name"].Value is null)
                    return string.Empty;

                string name = _fullHelpObject.Properties["Name"].Value.ToString();
                if (name is null)
                    return string.Empty;

                return name.Trim();
            }
        }

        /// <summary>
        /// Synopsis in the provider help info.
        /// </summary>
        /// <value>Synopsis in the provider help info</value>
        internal override string Synopsis
        {
            get
            {
                if (_fullHelpObject is null)
                    return string.Empty;

                if (_fullHelpObject.Properties["Synopsis"] is null)
                    return string.Empty;

                if (_fullHelpObject.Properties["Synopsis"].Value is null)
                    return string.Empty;

                string synopsis = _fullHelpObject.Properties["Synopsis"].Value.ToString();
                if (synopsis is null)
                    return string.Empty;

                return synopsis.Trim();
            }
        }

        /// <summary>
        /// Detailed description in the provider help info.
        /// </summary>
        /// <value>Detailed description in the provider help info</value>
        internal string DetailedDescription
        {
            get
            {
                if (this.FullHelp is null)
                    return string.Empty;

                if (this.FullHelp.Properties["DetailedDescription"] is null ||
                    this.FullHelp.Properties["DetailedDescription"].Value is null)
                {
                    return string.Empty;
                }

                IList descriptionItems = FullHelp.Properties["DetailedDescription"].Value as IList;
                if (descriptionItems is null || descriptionItems.Count == 0)
                {
                    return string.Empty;
                }

                // I think every provider description should atleast have 400 characters...
                // so starting with this assumption..I did an average of all the help content
                // available at the time of writing this code and came up with this number.
                Text.StringBuilder result = new Text.StringBuilder(400);
                foreach (object descriptionItem in descriptionItems)
                {
                    PSObject descriptionObject = PSObject.AsPSObject(descriptionItem);
                    if ((descriptionObject is null) ||
                        (descriptionObject.Properties["Text"] is null) ||
                        (descriptionObject.Properties["Text"].Value is null))
                    {
                        continue;
                    }

                    string text = descriptionObject.Properties["Text"].Value.ToString();
                    result.Append(text);
                    result.Append(Environment.NewLine);
                }

                return result.ToString().Trim();
            }
        }

        /// <summary>
        /// Help category for this provider help info, which is constantly HelpCategory.Provider.
        /// </summary>
        /// <value>Help category for this provider help info</value>
        internal override HelpCategory HelpCategory
        {
            get
            {
                return HelpCategory.Provider;
            }
        }

        private PSObject _fullHelpObject;

        /// <summary>
        /// Full help object for this provider help info.
        /// </summary>
        /// <value>Full help object for this provider help info</value>
        internal override PSObject FullHelp
        {
            get
            {
                return _fullHelpObject;
            }
        }

        /// <summary>
        /// Returns true if help content in help info matches the
        /// pattern contained in <paramref name="pattern"/>.
        /// The underlying code will usually run pattern.IsMatch() on
        /// content it wants to search.
        /// Provider help info looks for pattern in Synopsis and
        /// DetailedDescription.
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        internal override bool MatchPatternInContent(WildcardPattern pattern)
        {
            Diagnostics.Assert(pattern is not null, "pattern cannot be null");

            string synopsis = Synopsis;
            string detailedDescription = DetailedDescription;

            if (synopsis is null)
            {
                synopsis = string.Empty;
            }

            if (detailedDescription is null)
            {
                detailedDescription = string.Empty;
            }

            return pattern.IsMatch(synopsis) || pattern.IsMatch(detailedDescription);
        }

        #endregion

#if V2
        #region Cmdlet Help and Dynamic Parameter Help

        private Hashtable _cmdletHelps;

        /// <summary>
        /// Return the provider-specific cmdlet help based on input cmdletName.
        /// </summary>
        /// <param name="cmdletName">CmdletName on which to get provider-specific help.</param>
        /// <returns>An mshObject that contains provider-specific commandlet help.</returns>
        internal PSObject GetCmdletHelp(string cmdletName)
        {
            if (string.IsNullOrEmpty(cmdletName))
                return null;

            LoadCmdletHelps();

            if (_cmdletHelps is null)
                return null;

            return (PSObject)_cmdletHelps[cmdletName];
        }

        /// <summary>
        /// Load provider-specific commandlet helps from xmlNode stored in _fullHelpObject.
        /// Result will be stored in a hashtable.
        /// </summary>
        private void LoadCmdletHelps()
        {
            if (_cmdletHelps is not null)
                return;

            if (_fullHelpObject is null)
                return;

            _cmdletHelps = new Hashtable();

            if (_fullHelpObject.Properties["Cmdlets"] is null)
                return;

            PSObject cmdlets = (PSObject)_fullHelpObject.Properties["Cmdlets"].Value;

            if (cmdlets is null)
                return;

            if (cmdlets.Properties["Cmdlet"] is null ||
                cmdlets.Properties["Cmdlet"].Value is null)
                return;

            if (cmdlets.Properties["Cmdlet"].Value.GetType().Equals(typeof(PSObject[])))
            {
                PSObject[] cmdletHelpItems = (PSObject[])cmdlets.Properties["Cmdlet"].Value;

                for (int i = 0; i < cmdletHelpItems.Length; i++)
                {
                    if (cmdletHelpItems[i].Properties["Name"] is null
                        || cmdletHelpItems[i].Properties["Name"].Value is null)
                        return;

                    string name = ((PSObject)cmdletHelpItems[i].Properties["Name"].Value).ToString();

                    _cmdletHelps[name] = cmdletHelpItems[i];
                }
            }
            else if (cmdlets.Properties["Cmdlet"].Value.GetType().Equals(typeof(PSObject[])))
            {
                PSObject cmdletHelpItem = (PSObject)cmdlets.Properties["Cmdlet"].Value;

                string name = ((PSObject)cmdletHelpItem.Properties["Name"].Value).ToString();

                _cmdletHelps[name] = cmdletHelpItem;
            }
        }

        private Hashtable _dynamicParameterHelps;

        /// <summary>
        /// Return the provider-specific dynamic parameter help based on input parameter name.
        /// </summary>
        /// <param name="parameters">An array of parameters to retrieve help.</param>
        /// <returns>An array of mshObject that contains the parameter help.</returns>
        internal PSObject[] GetDynamicParameterHelp(string[] parameters)
        {
            if (parameters is null || parameters.Length == 0)
                return null;

            LoadDynamicParameterHelps();

            if (_dynamicParameterHelps is null)
                return null;

            ArrayList result = new ArrayList();

            for (int i = 0; i < parameters.Length; i++)
            {
                PSObject entry = (PSObject)_dynamicParameterHelps[parameters[i].ToLower()];

                if (entry is not null)
                    result.Add(entry);
            }

            return (PSObject[])result.ToArray(typeof(PSObject));
        }

        /// <summary>
        /// Load provider-specific dynamic parameter helps from xmlNode stored in _fullHelpObject.
        /// Result will be stored in a hashtable.
        /// </summary>
        private void LoadDynamicParameterHelps()
        {
            if (_dynamicParameterHelps is not null)
                return;

            if (_fullHelpObject is null)
                return;

            _dynamicParameterHelps = new Hashtable();

            if (_fullHelpObject.Properties["DynamicParameters"] is null)
                return;

            PSObject dynamicParameters = (PSObject)_fullHelpObject.Properties["DynamicParameters"].Value;

            if (dynamicParameters is null)
                return;

            if (dynamicParameters.Properties["DynamicParameter"] is null
                || dynamicParameters.Properties["DynamicParameter"].Value is null)
                return;

            if (dynamicParameters.Properties["DynamicParameter"].Value.GetType().Equals(typeof(PSObject[])))
            {
                PSObject[] dynamicParameterHelpItems = (PSObject[])dynamicParameters.Properties["DynamicParameter"].Value;

                for (int i = 0; i < dynamicParameterHelpItems.Length; i++)
                {
                    if (dynamicParameterHelpItems[i].Properties["Name"] is null
                        || dynamicParameterHelpItems[i].Properties["Name"].Value is null)
                        return;

                    string name = ((PSObject)dynamicParameterHelpItems[i].Properties["Name"].Value).ToString();

                    _dynamicParameterHelps[name] = dynamicParameterHelpItems[i];
                }
            }
            else if (dynamicParameters.Properties["DynamicParameter"].Value.GetType().Equals(typeof(PSObject[])))
            {
                PSObject dynamicParameterHelpItem = (PSObject)dynamicParameters.Properties["DynamicParameter"].Value;

                string name = ((PSObject)dynamicParameterHelpItem.Properties["Name"].Value).ToString();

                _dynamicParameterHelps[name] = dynamicParameterHelpItem;
            }
        }

        #endregion
#endif

        #region Load Help

        /// <summary>
        /// Create providerHelpInfo from an xmlNode.
        /// </summary>
        /// <param name="xmlNode">Xml node that contains the provider help info.</param>
        /// <returns>The providerHelpInfo object created.</returns>
        internal static ProviderHelpInfo Load(XmlNode xmlNode)
        {
            ProviderHelpInfo providerHelpInfo = new ProviderHelpInfo(xmlNode);

            if (string.IsNullOrEmpty(providerHelpInfo.Name))
                return null;

            providerHelpInfo.AddCommonHelpProperties();

            return providerHelpInfo;
        }

        #endregion
    }
}

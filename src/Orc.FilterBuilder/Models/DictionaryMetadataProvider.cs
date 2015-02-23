﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DictionaryMetadataProvider.cs" company="Wild Gums">
//   Copyright (c) 2008 - 2015 Wild Gums. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
namespace Orc.FilterBuilder.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Catel;
    using Catel.Reflection;

    public class DictionaryMetadataProvider : IMetadataProvider
    {
        private List<IPropertyMetadata> _properties;

        public DictionaryMetadataProvider()
        {
            _properties = new List<IPropertyMetadata>();
        }

        public DictionaryMetadataProvider(Dictionary<string, Type> dictionarySchema)
        {
            Argument.IsNotNull(() => dictionarySchema);
            _properties = dictionarySchema.Select(kvp => new DictionaryEntryMetadata(kvp.Key, kvp.Value)).Cast<IPropertyMetadata>().ToList();
        }

        public List<IPropertyMetadata> Properties
        {
            get { return _properties; }
        }

        public bool IsAssignableFromEx(IMetadataProvider otherProvider)
        {
            // suppose that anything can be converted into empty dictionary so the source can be really schemaless
            return true;
        }

        public string SerializeState()
        {
            var propsPresentation = new List<string>();

            foreach (var propertyMetadata in _properties)
            {
                var prop = propertyMetadata as DictionaryEntryMetadata;
                propsPresentation.Add(string.Format("{0}:{1}", prop.Name, prop.Type.FullName)); 
            }


            return string.Join(";", propsPresentation);
        }

        public void DeserializeState(string contentAsString)
        {
            _properties.Clear();
            var entries = contentAsString.Split(';');
            foreach (var sentry in entries)
            {
                var kvp = sentry.Split(':');
                _properties.Add(new DictionaryEntryMetadata(kvp[0], TypeCache.GetType(kvp[1])));
            }
        }
    }
}
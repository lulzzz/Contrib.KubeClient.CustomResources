using System;
using KubeClient.Models.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Contrib.KubeClient.CustomResources
{
    /// <summary>
    /// Identifies a specific Kubernetes Custom Resource type.
    /// </summary>
    public class CustomResourceDefinition
    {
        /// <summary>
        /// The Kubernetes API Version of the resource (&lt;apiGroupName&gt;/&lt;version&gt;, e.g. 'your.company/v1').
        /// </summary>
        public string ApiVersion { get; }

        /// <summary>
        /// The plural name of the resource (see <code>spec.names.plural</code>).
        /// </summary>
        public string PluralName { get; }

        /// <summary>
        /// The singular upper-case name of the resource (see <code>spec.names.kind</code>).
        /// </summary>
        public string Kind { get; }

        /// <summary>
        /// Settings to use when serializing/deserializing this type of Custom Resource.
        /// </summary>
        public JsonSerializerSettings SerializerSettings { get; } = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters =
            {
                new StringEnumConverter(),
                new Int32OrStringV1Converter()
            }
        };

        /// <summary>
        /// Creates a new Kubernetes Custom Resource Definition.
        /// </summary>
        /// <param name="apiVersion">The Kubernetes API Version of the resource (&lt;apiGroupName&gt;/&lt;version&gt;, e.g. 'your.company/v1').</param>
        /// <param name="pluralName">The plural name of the resource (see <code>spec.names.plural</code>).</param>
        /// <param name="kind">The singular upper-case name of the resource (see <code>spec.names.kind</code>).</param>
        public CustomResourceDefinition(string apiVersion, string pluralName, string kind)
        {
            ApiVersion = apiVersion ?? throw new ArgumentNullException(nameof(apiVersion));
            PluralName = pluralName ?? throw new ArgumentNullException(nameof(pluralName));
            Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        }

        public override string ToString() => $"{ApiVersion}/{PluralName}";
    }
}

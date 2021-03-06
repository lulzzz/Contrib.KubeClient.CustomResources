using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using HTTPlease;
using HTTPlease.Formatters.Json;
using KubeClient;
using KubeClient.Models;
using KubeClient.ResourceClients;
using Microsoft.AspNetCore.JsonPatch;

namespace Contrib.KubeClient.CustomResources
{
    /// <summary>
    /// Client for Kubernetes Custom Resources of a specific type.
    /// </summary>
    /// <typeparam name="TResource">The Kubernetes Custom Resource DTO type.</typeparam>
    public class CustomResourceClient<TResource> : KubeResourceClient, ICustomResourceClient<TResource>
        where TResource : CustomResource, new()
    {
        private readonly CustomResourceDefinition _crd;

        /// <summary>
        /// Creates a Kubernetes Custom Resources client.
        /// </summary>
        /// <param name="client">The kube api client to be used.</param>
        public CustomResourceClient(IKubeApiClient client)
            : base(client)
        {
            _crd = new TResource().Definition;
        }

        /// <summary>
        /// The timeout for watching kubernetes event streams, after which the stream will be closed automatically.
        /// </summary>
        /// <remarks>The Kubernetes API stores events for 5 minutes by default. This value should be lower than that to avoid excessive cache rebuilding.</remarks>
        protected virtual TimeSpan WatchTimeout => TimeSpan.FromMinutes(4);

        public virtual IObservable<IResourceEventV1<TResource>> Watch(string @namespace = "", string resourceVersionOffset = "0")
        {
            var httpRequest = CreateBaseRequest(@namespace)
                             .WithQueryParameter("watch", true)
                             .WithQueryParameter("resourceVersion", resourceVersionOffset)
                             .WithQueryParameter("timeoutSeconds", WatchTimeout.TotalSeconds);

            return ObserveEvents<TResource>(httpRequest, operationDescription: $"watch '{_crd.PluralName}'")
               .Timeout(WatchTimeout + TimeSpan.FromSeconds(30)); // Enforce client-side timeout in addition to server-side
        }

        public async Task<CustomResourceList<TResource>> ListAsync(string labelSelector = null, string @namespace = null, CancellationToken cancellationToken = default)
        {
            var httpRequest = CreateBaseRequest(@namespace);
            if (!string.IsNullOrWhiteSpace(labelSelector))
                httpRequest = httpRequest.WithQueryParameter("labelSelector", labelSelector);

            return await GetResourceList<CustomResourceList<TResource>>(httpRequest, cancellationToken);
        }

        public virtual async Task<TResource> ReadAsync(string resourceName, string @namespace = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(resourceName)) throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespaces.", nameof(resourceName));

            var httpRequest = CreateBaseRequest(@namespace).WithRelativeUri(resourceName);
            var resource = await GetSingleResource<TResource>(httpRequest, cancellationToken);

            var error = resource.SerializationErrors.FirstOrDefault();
            if (error != null) throw error.Error;

            return resource;
        }

        public virtual async Task<TResource> CreateAsync(TResource resource, CancellationToken cancellationToken = default)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));

            var httpRequest = CreateBaseRequest(resource.Metadata.Namespace);
            var responseMessage = await Http.PostAsJsonAsync(httpRequest, resource, cancellationToken);
            return await responseMessage.ReadContentAsAsync<TResource, StatusV1>();
        }

        public virtual async Task<TResource> UpdateAsync(string name, Action<JsonPatchDocument<TResource>> patchAction, string @namespace = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'name'.", nameof(name));
            if (patchAction == null)
                throw new ArgumentNullException(nameof(patchAction));

            var httpRequest = CreateBaseRequest(@namespace).WithRelativeUri(name);
            return await PatchResource(patchAction, httpRequest, cancellationToken);
        }

        public virtual async Task<TResource> ReplaceAsync(TResource resource, CancellationToken cancellationToken = default)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            if (resource.Metadata.ResourceVersion == null) throw new ArgumentException($"{nameof(KubeResourceV1.Metadata)}.{nameof(ObjectMetaV1.ResourceVersion)} must be set. This is the case only when the object was retrieved from the Kubernetes API.", nameof(resource));

            var httpRequest = CreateBaseRequest(resource.Metadata.Namespace).WithRelativeUri(resource.Metadata.Name);
            var responseMessage = await Http.PutAsJsonAsync(httpRequest, resource, cancellationToken);
            return await responseMessage.ReadContentAsAsync<TResource, StatusV1>();
        }

        public virtual async Task<TResource> DeleteAsync(string resourceName, string @namespace = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(resourceName)) throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespaces.", nameof(resourceName));

            var deleteOptions = new DeleteOptionsV1 {PropagationPolicy = DeletePropagationPolicy.Foreground};
            var httpRequest = CreateBaseRequest(@namespace).WithRelativeUri(resourceName);
            var responseMessage = await Http.DeleteAsJsonAsync(httpRequest, deleteOptions, cancellationToken);

            return await responseMessage.ReadContentAsAsync<TResource, StatusV1>();
        }

        private HttpRequest CreateBaseRequest(string @namespace)
        {
            var httpRequest = HttpRequest.Create(new Uri($"/apis/{_crd.ApiVersion}", UriKind.Relative))
                                         .ExpectJson()
                                         .WithFormatter(new JsonFormatter()
                                          {
                                              SerializerSettings = _crd.SerializerSettings,
                                              SupportedMediaTypes =
                                              {
                                                  PatchMediaType,
                                                  MergePatchMediaType
                                              }
                                          });

            if (!string.IsNullOrWhiteSpace(@namespace))
                httpRequest = httpRequest.WithRelativeUri($"namespaces/{@namespace}/");

            return httpRequest.WithRelativeUri(_crd.PluralName);
        }
    }
}

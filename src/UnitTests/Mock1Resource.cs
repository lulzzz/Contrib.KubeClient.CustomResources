using Microsoft.AspNetCore.JsonPatch;

namespace Contrib.KubeClient.CustomResources
{
    public class Mock1Resource : CustomResource<int>, IPayloadPatchable<Mock1Resource>
    {
        public new static CustomResourceDefinition Definition { get; } = new CustomResourceDefinition(apiVersion: "example.com/v1", pluralName: "mock1s", kind: "Mock");

        public Mock1Resource()
            : base(Definition)
        {}

        public Mock1Resource(string @namespace = null, string name = null, int spec = 0)
            : base(Definition, @namespace, name, spec)
        {
            Metadata.Uid = $"{@namespace}/{name}";
            Metadata.ResourceVersion = spec.ToString();
        }

        public void ToPayloadPatch(JsonPatchDocument<Mock1Resource> patch)
            => patch.Replace(x => x.Spec, Spec);
    }
}

using Robust.Shared.IoC;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossMechanicParametersSerializer : ITypeSerializer<MappingDataNode, MappingDataNode>, ITypeCopyCreator<MappingDataNode>
{
    public MappingDataNode Read(ISerializationManager serialization, MappingDataNode node, IDependencyCollection dependencies,
        SerializationHookContext hookCtx, ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<MappingDataNode>? instanceProvider = null) => node.Copy();

    public DataNode Write(ISerializationManager serialization, MappingDataNode value, IDependencyCollection dependencies,
        bool alwaysWrite = false, ISerializationContext? context = null) => value.Copy();

    public ValidationNode Validate(ISerializationManager serialization, MappingDataNode node, IDependencyCollection dependencies,
        ISerializationContext? context = null) => new ValidatedValueNode(node);   // the owning module validates its own fields

    public MappingDataNode CreateCopy(ISerializationManager serialization, MappingDataNode source, IDependencyCollection dependencies,
        SerializationHookContext hookCtx, ISerializationContext? context = null) => source.Copy();
}

using FabInspector.Core;
using FabInspector.Operators;
using Ric.Operators;

namespace FabInspector.Web.Services;

/// <summary>
/// Registers the JsonLogic operator catalogue and report renderer used by the
/// inspection engine. Mirrors <c>FabInspector.CLI.Program.ConfigureSharedServices</c>;
/// kept here as a copy because the CLI helper is <c>internal</c>.
/// </summary>
public static class OperatorRegistration
{
    public static IServiceCollection AddFabInspectorOperators(this IServiceCollection services)
    {
        var registries = new List<JsonLogicOperatorRegistry>
        {
            new JsonLogicOperatorRegistry(
                new RicSerializerContext(),
                new IJsonLogicOperator[]
                {
                    new CountOperator(),
                    new DrillVariableOperator(),
                    new FileSizeOperator(),
                    new FileTextSearchCountOperator(),
                    new IsNullOrEmptyOperator(),
                    new PartInfoOperator(),
                    new PartOperator(),
                    new PathOperator(),
                    new QueryOperator(),
                    new SetDifferenceOperator(),
                    new SetEqualOperator(),
                    new SetIntersectionOperator(),
                    new SetSymmetricDifferenceOperator(),
                    new SetUnionOperator(),
                    new StringContainsOperator(),
                    new ToRecordOperator(),
                    new ToStringOperator(),
                    new FromYamlFileOperator(),
                    new KeysOperator(),
                    new ValuesOperator(),
                    new DistinctOperator(),
                    new TypeOfOperator(),
                    new HasPropOperator(),
                    new StringSplitOperator(),
                    new StringJoinOperator(),
                    new RegexExtractOperator(),
                    new CoalesceOperator(),
                    new SliceOperator(),
                    new NowOperator(),
                    new DateDiffOperator(),
                    new LetOperator()
                }),
            new JsonLogicOperatorRegistry(
                new FabInspectorSerializerContext(),
                new IJsonLogicOperator[]
                {
                    new RectangleOverlapOperator(),
                    new DaxQueryOperator(),
                    new SqlQueryOperator(),
                    new ApiGetOperator(),
                    new DfsGetOperator(),
                    new ScannerApiOperator()
                })
        };

        services.AddSingleton<IEnumerable<JsonLogicOperatorRegistry>>(registries);
        services.AddSingleton<IReportPageWireframeRenderer, FabInspector.ImageLibrary.ReportPageWireframeRenderer>();
        return services;
    }
}

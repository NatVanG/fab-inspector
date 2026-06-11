using FabInspector.Core;
using FabInspector.ImageLibrary;
using FabInspector.Operators;
using Microsoft.Extensions.DependencyInjection;
using Ric.Operators;

namespace FabInspector.AvaloniaUI;

public static class AppServices
{
    public static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        var registries = new List<JsonLogicOperatorRegistry>
        {
            new(
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
                    new LetOperator(),
                    new RectangleOverlapOperator()
                }),
            new(
                new FabInspectorSerializerContext(),
                new IJsonLogicOperator[]
                {
                    new DaxQueryOperator(),
                    new SqlQueryOperator(),
                    new ApiGetOperator(),
                    new DfsGetOperator(),
                    new ScannerApiOperator()
                })
        };

        services.AddSingleton<IEnumerable<JsonLogicOperatorRegistry>>(registries);
        services.AddSingleton<IReportPageWireframeRenderer, ReportPageWireframeRenderer>();

        return services.BuildServiceProvider();
    }
}

using Prism.Features.Experiments.Domain;

namespace Prism.Tests.Unit.Experiments;

public sealed class RunSweepHandlerTests
{
    /// <summary>
    /// Mirrors the cartesian product logic from RunSweepHandler.GenerateCombinations
    /// so we can unit-test the combination generation in isolation.
    /// </summary>
    private static List<RunParameters> GenerateCombinations(
        List<double> temperatureValues,
        List<double> topPValues,
        List<int> maxTokensValues)
    {
        List<double> temperatures = temperatureValues.Count > 0
            ? temperatureValues
            : [0.7];
        List<double> topPs = topPValues.Count > 0
            ? topPValues
            : [1.0];
        List<int> maxTokensList = maxTokensValues.Count > 0
            ? maxTokensValues
            : [2048];

        List<RunParameters> combos = [];

        foreach (double temp in temperatures)
        {
            foreach (double topP in topPs)
            {
                foreach (int maxTokens in maxTokensList)
                {
                    combos.Add(new RunParameters
                    {
                        Temperature = temp,
                        TopP = topP,
                        MaxTokens = maxTokens
                    });
                }
            }
        }

        return combos;
    }

    [Fact]
    public void CartesianProduct_SingleParam_ProducesCorrectCount()
    {
        List<double> temperatures = [0.1, 0.5, 0.7, 1.0];
        List<double> topPs = [0.9];
        List<int> maxTokens = [2048];

        List<RunParameters> result = GenerateCombinations(temperatures, topPs, maxTokens);

        result.Should().HaveCount(4);
        result[0].Temperature.Should().Be(0.1);
        result[1].Temperature.Should().Be(0.5);
        result[2].Temperature.Should().Be(0.7);
        result[3].Temperature.Should().Be(1.0);
        result.Should().OnlyContain(r => r.TopP == 0.9);
        result.Should().OnlyContain(r => r.MaxTokens == 2048);
    }

    [Fact]
    public void CartesianProduct_MultiParam_ProducesCorrectCount()
    {
        List<double> temperatures = [0.3, 0.7, 1.0];
        List<double> topPs = [0.8, 1.0];
        List<int> maxTokens = [512];

        List<RunParameters> result = GenerateCombinations(temperatures, topPs, maxTokens);

        // 3 temperatures x 2 topPs x 1 maxTokens = 6
        result.Should().HaveCount(6);

        // Verify all combinations exist
        result.Should().Contain(r => r.Temperature == 0.3 && r.TopP == 0.8);
        result.Should().Contain(r => r.Temperature == 0.3 && r.TopP == 1.0);
        result.Should().Contain(r => r.Temperature == 0.7 && r.TopP == 0.8);
        result.Should().Contain(r => r.Temperature == 0.7 && r.TopP == 1.0);
        result.Should().Contain(r => r.Temperature == 1.0 && r.TopP == 0.8);
        result.Should().Contain(r => r.Temperature == 1.0 && r.TopP == 1.0);
    }

    [Fact]
    public void CartesianProduct_EmptyArrays_DefaultsToSingleValue()
    {
        List<double> temperatures = [];
        List<double> topPs = [];
        List<int> maxTokens = [];

        List<RunParameters> result = GenerateCombinations(temperatures, topPs, maxTokens);

        result.Should().HaveCount(1);
        result[0].Temperature.Should().Be(0.7);
        result[0].TopP.Should().Be(1.0);
        result[0].MaxTokens.Should().Be(2048);
    }
}

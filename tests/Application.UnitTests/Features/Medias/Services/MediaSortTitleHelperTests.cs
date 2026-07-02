using K7.Server.Application.Features.Medias.Services;

namespace K7.Server.Application.UnitTests.Features.Medias.Services;

public class MediaSortTitleHelperTests
{
    [TestCase(null, null)]
    [TestCase("", null)]
    [TestCase("   ", null)]
    [TestCase("Inception", "Inception")]
    [TestCase("The Matrix", "Matrix, The")]
    [TestCase("the matrix", "matrix, the")]
    [TestCase("A Beautiful Mind", "Beautiful Mind, A")]
    [TestCase("An American Werewolf in London", "American Werewolf in London, An")]
    [TestCase("Le Seigneur des anneaux", "Seigneur des anneaux, Le")]
    [TestCase("La La Land", "La Land, La")]
    [TestCase("Les Misérables", "Misérables, Les")]
    [TestCase("Un homme ideal", "homme ideal, Un")]
    [TestCase("Une affaire de famille", "affaire de famille, Une")]
    [TestCase("Des hommes et des dieux", "hommes et des dieux, Des")]
    [TestCase("L'Arnacoeur", "Arnacoeur, L'")]
    [TestCase("L\u2019Amélie", "Amélie, L\u2019")]
    public void Compute_ShouldReturnExpectedSortTitle(string? input, string? expected)
    {
        MediaSortTitleHelper.Compute(input).Should().Be(expected);
    }
}

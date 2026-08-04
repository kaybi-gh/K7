using K7.Server.Application.Features.Medias.Services;

namespace K7.Server.Application.UnitTests.Features.Medias.Services;

public class MediaSortTitleHelperTests
{
    [TestCase(null, null)]
    [TestCase("", null)]
    [TestCase("   ", null)]
    [TestCase("Inception", "Inception")]
    [TestCase("The Matrix", "Matrix, The")]
    [TestCase("the matrix", "Matrix, the")]
    [TestCase("A Beautiful Mind", "Beautiful Mind, A")]
    [TestCase("An American Werewolf in London", "American Werewolf in London, An")]
    [TestCase("Le Seigneur des anneaux", "Seigneur des anneaux, Le")]
    [TestCase("La La Land", "La Land, La")]
    [TestCase("Les Misérables", "Miserables, Les")]
    [TestCase("Un homme ideal", "Homme ideal, Un")]
    [TestCase("Une affaire de famille", "Affaire de famille, Une")]
    [TestCase("Des hommes et des dieux", "Hommes et des dieux, Des")]
    [TestCase("Des hommes d'honneur", "Hommes d'honneur, Des")]
    [TestCase("Un p'tit truc en plus", "P'tit truc en plus, Un")]
    [TestCase("Les aventures de Pinocchio", "Aventures de Pinocchio, Les")]
    [TestCase("L'Arnacoeur", "Arnacoeur, L'")]
    [TestCase("L\u2019Amélie", "Amelie, L\u2019")]
    [TestCase("Été 85", "Ete 85")]
    [TestCase("Ça", "Ca")]
    [TestCase("À l'intérieur", "A l'interieur")]
    public void Compute_ShouldReturnExpectedSortTitle(string? input, string? expected)
    {
        MediaSortTitleHelper.Compute(input).Should().Be(expected);
    }

    [Test]
    public void Compute_ShouldStripInvisibleCharacters()
    {
        var input = "\uFEFF\u200BDes\u00AD hommes d'honneur\u200B";
        MediaSortTitleHelper.Compute(input).Should().Be("Hommes d'honneur, Des");
    }

    [Test]
    public void Compute_ShouldStripUppercaseDiacritics()
    {
        MediaSortTitleHelper.Compute("Éléphant").Should().Be("Elephant");
        MediaSortTitleHelper.Compute("Œuvre").Should().Be("Œuvre");
    }

    [Test]
    public void Compute_ShouldKeepLeadingCapital_WhenAlreadyUppercase()
    {
        MediaSortTitleHelper.Compute("Matrix").Should().Be("Matrix");
    }
}

namespace K7.Server.Domain.Restrictions;

public static class AgeCalculator
{
    public static int GetCompletedYears(DateOnly birthDate, DateOnly today)
    {
        if (today < birthDate)
            return 0;

        var age = today.Year - birthDate.Year;
        if (birthDate.AddYears(age) > today)
            age--;

        return age;
    }
}

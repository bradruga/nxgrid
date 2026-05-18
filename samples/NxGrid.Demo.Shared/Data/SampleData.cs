namespace NxGrid.Demo.Shared.Data;

public static class SampleData
{
    public static IReadOnlyList<Person> People { get; } = new[]
    {
        new Person { Id = 1, FirstName = "Alice",  LastName = "Johnson",  Age = 32, Department = "Engineering" },
        new Person { Id = 2, FirstName = "Bob",    LastName = "Smith",    Age = 45, Department = "Marketing"   },
        new Person { Id = 3, FirstName = "Carol",  LastName = "Williams", Age = 28, Department = "Engineering" },
        new Person { Id = 4, FirstName = "David",  LastName = "Brown",    Age = 51, Department = "Finance"     },
        new Person { Id = 5, FirstName = "Eve",    LastName = "Davis",    Age = 37, Department = "Marketing"   },
        new Person { Id = 6, FirstName = "Frank",  LastName = "Miller",   Age = 29, Department = "Engineering" },
        new Person { Id = 7, FirstName = "Grace",  LastName = "Wilson",   Age = 44, Department = "Finance"     },
        new Person { Id = 8, FirstName = "Henry",  LastName = "Moore",    Age = 38, Department = "HR"          },
    };

    private static readonly string[] FirstNames =
    [
        "Alice", "Bob", "Carol", "David", "Eve", "Frank", "Grace", "Henry",
        "Iris", "Jack", "Karen", "Leo", "Mia", "Noah", "Olivia", "Paul",
        "Quinn", "Rose", "Sam", "Tara", "Uma", "Vince", "Wendy", "Xander",
        "Yara", "Zoe"
    ];

    private static readonly string[] LastNames =
    [
        "Johnson", "Smith", "Williams", "Brown", "Davis", "Miller", "Wilson",
        "Moore", "Taylor", "Anderson", "Thomas", "Jackson", "White", "Harris",
        "Martin", "Thompson", "Garcia", "Martinez", "Robinson", "Clark"
    ];

    private static readonly string[] Departments =
        ["Engineering", "Finance", "HR", "Marketing", "Sales", "Legal"];

    public static List<Person> GetPeopleCopy() =>
        People.Select(p => new Person
        {
            Id = p.Id,
            FirstName = p.FirstName,
            LastName = p.LastName,
            Age = p.Age,
            Department = p.Department
        }).ToList();

    public static List<WideRow> GenerateWideRows(int count) =>
        Enumerable.Range(1, count).Select(i => new WideRow(i)).ToList();

    public static List<Person> GeneratePeople(int count)
    {
        var rng = new Random(42);
        return Enumerable.Range(1, count).Select(i => new Person
        {
            Id = i,
            FirstName = FirstNames[rng.Next(FirstNames.Length)],
            LastName = LastNames[rng.Next(LastNames.Length)],
            Age = rng.Next(22, 65),
            Department = Departments[rng.Next(Departments.Length)]
        }).ToList();
    }
}

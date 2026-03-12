using System;
using System.Collections.Generic;
using System.Linq;

namespace MVCAllOptions.AI.Data;

/// <summary>
/// Represents a book in the bookstore catalogue.
/// </summary>
public record BookRecord(
    Guid Id,
    string Name,
    string Genre,
    DateTime PublishDate,
    float Price,
    string Author,
    string Description);

/// <summary>
/// In-memory bookstore catalogue — mirrors the ABP domain model.
/// </summary>
public static class BookstoreData
{
    public static readonly IReadOnlyList<BookRecord> Catalogue = new List<BookRecord>
    {
        new(Guid.NewGuid(), "Brave New World",         "Dystopia",       new DateTime(1932, 8, 30),  14.99f, "Aldous Huxley",
            "A chilling vision of a future society where human beings are engineered and conditioned."),

        new(Guid.NewGuid(), "The Hitchhiker's Guide",  "ScienceFiction", new DateTime(1979, 10, 12), 12.99f, "Douglas Adams",
            "The absurdist comic science fiction series following Arthur Dent after Earth is demolished."),

        new(Guid.NewGuid(), "1984",                    "Dystopia",       new DateTime(1949, 6, 8),   11.99f, "George Orwell",
            "A dystopian novel about totalitarianism, surveillance, and the erasure of individuality."),

        new(Guid.NewGuid(), "Foundation",              "ScienceFiction", new DateTime(1951, 5, 1),   15.99f, "Isaac Asimov",
            "The fall of a galactic empire as mathematically predicted by psychohistorian Hari Seldon."),

        new(Guid.NewGuid(), "Dune",                    "ScienceFiction", new DateTime(1965, 8, 1),   18.99f, "Frank Herbert",
            "Epic interstellar adventure following Paul Atreides on the desert planet Arrakis."),

        new(Guid.NewGuid(), "The Shining",             "Horror",         new DateTime(1977, 1, 28),  13.99f, "Stephen King",
            "A winter caretaker slowly goes mad inside a haunted Colorado hotel."),

        new(Guid.NewGuid(), "It",                      "Horror",         new DateTime(1986, 9, 15),  19.99f, "Stephen King",
            "Seven outcast children face an ancient, shape-shifting monster that preys on their fears."),

        new(Guid.NewGuid(), "The Lord of the Rings",   "Adventure",      new DateTime(1954, 7, 29),  29.99f, "J.R.R. Tolkien",
            "The definitive fantasy epic following hobbit Frodo Baggins on his quest to destroy the One Ring."),

        new(Guid.NewGuid(), "Steve Jobs",              "Biography",      new DateTime(2011, 10, 24), 16.99f, "Walter Isaacson",
            "The exclusive biography of the visionary co-founder of Apple Inc."),

        new(Guid.NewGuid(), "A Brief History of Time", "Science",        new DateTime(1988, 4, 1),   14.99f, "Stephen Hawking",
            "A landmark volume in science writing covering cosmology, black holes, and the nature of time."),

        new(Guid.NewGuid(), "The Great Gatsby",        "Adventure",      new DateTime(1925, 4, 10),  10.99f, "F. Scott Fitzgerald",
            "A portrayal of the Jazz Age through the story of the mysterious millionaire Jay Gatsby."),

        new(Guid.NewGuid(), "The Road",                "Adventure",      new DateTime(2006, 9, 26),  13.99f, "Cormac McCarthy",
            "A father and son walk alone through post-apocalyptic America."),
    };

    // ── Query helpers ─────────────────────────────────────────────────────────

    public static IEnumerable<BookRecord> SearchByName(string query) =>
        Catalogue.Where(b => b.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<BookRecord> GetByGenre(string genre) =>
        Catalogue.Where(b => b.Genre.Equals(genre, StringComparison.OrdinalIgnoreCase));

    public static BookRecord? GetByNameExact(string name) =>
        Catalogue.FirstOrDefault(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<BookRecord> GetByPriceRange(float min, float max) =>
        Catalogue.Where(b => b.Price >= min && b.Price <= max);

    public static IEnumerable<string> GetAllGenres() =>
        Catalogue.Select(b => b.Genre).Distinct().Order();
}

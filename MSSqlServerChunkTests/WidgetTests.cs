using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Xunit;

namespace MSSqlServerChunkTests
{
    public class WidgetTests
    {
        [Fact]
        public void Should_return_chunked_data()
        {
            using (var context = new WidgetDbContextFactory().CreateDbContext(args: null))
            {
                context.Database.Migrate();

                for (var i = 0; i < 100; i++)
                {
                    context.Widgets.Add(new Widget()
                    {
                        Name = Guid.NewGuid().ToString()
                    });
                }

                context.SaveChanges();

                using (var command = context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = "select top 1 widgets = (select * from widgets for json path) from widgets for json path, without_array_wrapper;";
                    command.Connection.Open();

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            var results = new List<string>();

                            while (reader.Read())
                            {
                                results.Add(reader.GetString(0));
                            }

                            Assert.NotEqual(1, results.Count);

                            Assert.Throws<JsonReaderException>(() => JsonConvert.DeserializeObject<WidgetResponse>(results[0]));

                            results.RemoveAt(results.Count - 1);

                            Assert.All(results, x => Assert.Equal(2033, x.Length));
                        }
                    }
                }

                context.Database.EnsureDeleted();
            }
        }

        [Fact]
        public void Should_return_singular_data()
        {
            using (var context = new WidgetDbContextFactory().CreateDbContext(null))
            {
                context.Database.Migrate();

                for (var i = 0; i < 5000; i++)
                {
                    context.Widgets.Add(new Widget()
                    {
                        Name = Guid.NewGuid().ToString()
                    });
                }

                context.SaveChanges();

                using (var command = context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = "select (select * from widgets for json path, root('widgets'));";
                    command.Connection.Open();

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            var result = string.Empty;

                            while (reader.Read())
                            {
                                result = reader.GetString(0);
                            }

                            var response = JsonConvert.DeserializeObject<WidgetResponse>(result);

                            Assert.Equal(5000, response.Widgets.Count);
                        }
                    }
                }

                context.Database.EnsureDeleted();
            }
        }
    }

    public class WidgetContext : DbContext
    {
        public WidgetContext(DbContextOptions<WidgetContext> options)
            : base(options)
        { }

        public DbSet<Widget> Widgets { get; set; }
    }

    public class WidgetDbContextFactory : IDesignTimeDbContextFactory<WidgetContext>
    {
        public WidgetContext CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<WidgetContext>();

            builder.UseSqlServer($"Server=(LocalDb)\\v13.0; Database=test-{Guid.NewGuid()}; Integrated Security=True;");

            return new WidgetContext(builder.Options);
        }
    }

    public class Widget
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class WidgetResponse
    {
        public IReadOnlyCollection<Widget> Widgets { get; set; } = Array.Empty<Widget>();
    }
}

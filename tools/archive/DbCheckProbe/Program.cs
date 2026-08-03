using Microsoft.EntityFrameworkCore;
using WEMP.Infrastructure.Data;

var options = new DbContextOptionsBuilder<WempDbContext>()
    .UseSqlite(WempDatabase.CreateConnectionString())
    .Options;

using var db = new WempDbContext(options);
Console.WriteLine($"env_templates count: {db.EnvTemplates.Count()}");
foreach (var t in db.EnvTemplates.AsNoTracking())
{
    Console.WriteLine($"  #{t.Id} key={t.TemplateKey} name={t.Name} builtin={t.BuiltIn} enabled={t.Enabled}");
}

Console.WriteLine($"env_instances count: {db.EnvInstances.Count()}");
foreach (var i in db.EnvInstances.AsNoTracking())
{
    Console.WriteLine($"  #{i.Id} templateId={i.TemplateId} name={i.Name} status={i.Status}");
}

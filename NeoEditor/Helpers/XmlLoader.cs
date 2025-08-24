using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Data.Context;
using NeoEditor.Data.Models;
using NeoEditor.Helpers.Converters;
using NeoEditor.ViewModels.Data;

namespace NeoEditor.Helpers;

public interface ILoadingCollection
{
    public void Add(dynamic? obj);

    public int Count();

    public Task Clean();


    public void BulkInsert();
}

public class LoadingCollection<T>(DbSet<T> db) : ILoadingCollection where T : class
{
    public Collection<T> Collection = new(db.ToList());

    public void Add(dynamic? obj)
    {
        Collection.Add(obj);
    }

    public int Count()
    {
        return Collection.Count;
    }

    public async Task Clean()
    {
        await db.LoadAsync();
        await db.ExecuteDeleteAsync();
    }

    public void BulkInsert()
    {
        db.AddRange(Collection.AsEnumerable());
    }
}

public class BulkCollection(NeoContext db)
{
    public readonly Dictionary<Type, ILoadingCollection> Collections = new()
    {
        { typeof(attackmode), new LoadingCollection<attackmode>(db.attackmodes) },
        { typeof(barterhex), new LoadingCollection<barterhex>(db.barterhexes) },
        { typeof(battlemove), new LoadingCollection<battlemove>(db.battlemoves) },
        { typeof(camptype), new LoadingCollection<camptype>(db.camptypes) },
        { typeof(chargeprofile), new LoadingCollection<chargeprofile>(db.chargeprofiles) },
        { typeof(condition), new LoadingCollection<condition>(db.conditions) },
        { typeof(containertype), new LoadingCollection<containertype>(db.containertypes) },
        { typeof(creature), new LoadingCollection<creature>(db.creatures) },
        { typeof(creaturesource), new LoadingCollection<creaturesource>(db.creaturesources) },
        { typeof(datafile), new LoadingCollection<datafile>(db.datafiles) },
        { typeof(dmcplace), new LoadingCollection<dmcplace>(db.dmcplaces) },
        { typeof(encounter), new LoadingCollection<encounter>(db.encounters) },
        { typeof(encountertrigger), new LoadingCollection<encountertrigger>(db.encountertriggers) },
        { typeof(faction), new LoadingCollection<faction>(db.factions) },
        { typeof(forbiddenhex), new LoadingCollection<forbiddenhex>(db.forbiddenhexes) },
        { typeof(gamevar), new LoadingCollection<gamevar>(db.gamevars) },
        { typeof(headline), new LoadingCollection<headline>(db.headlines) },
        { typeof(hextype), new LoadingCollection<hextype>(db.hextypes) },
        { typeof(image), new LoadingCollection<image>(db.images) },
        { typeof(ingredient), new LoadingCollection<ingredient>(db.ingredients) },
        { typeof(itemprop), new LoadingCollection<itemprop>(db.itemprops) },
        { typeof(itemtype), new LoadingCollection<itemtype>(db.itemtypes) },
        { typeof(map), new LoadingCollection<map>(db.maps) },
        { typeof(recipe), new LoadingCollection<recipe>(db.recipes) },
        { typeof(treasuretable), new LoadingCollection<treasuretable>(db.treasuretables) }
    };

    public void Add(dynamic? obj)
    {
        Collections[obj?.GetType()].Add(obj);
    }

    public int Count(string name)
    {
        return Collections[DictionaryModelConverter.GetType(name)].Count();
    }

    public async Task BulkInsert()
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            foreach (var collection in Collections.Values) collection.BulkInsert();
        });


        await db.SaveChangesAsync();
    }
}

public class XmlLoader
{
    private readonly BulkCollection _bulkCollection;
    private readonly Dictionary<string, int> _cntDictory;
    private readonly NeoContext _db;

    public XmlLoader(NeoContext db)
    {
        _bulkCollection = new BulkCollection(db);
        _cntDictory = new Dictionary<string, int>();
        _db = db;
    }

    public int Idx { get; set; }

    public async Task Clean()
    {
        Idx = 0;
        // _cntDictory.Clear();
        await Truncate();
    }

    public async Task Truncate()
    {
        foreach (var bulkCollectionCollection in _bulkCollection.Collections.Values)
            await bulkCollectionCollection.Clean();
        await _db.SaveChangesAsync();
    }

    public async Task CleanEncodingHeader(string xmlFilePath)
    {
        // 读取文件第一行，替换utf8为utf-8，保存
        await using (var f2 = new StreamWriter(xmlFilePath + ".tmp"))
        {
            using var f = new StreamReader(xmlFilePath);
            var firstLine = await f.ReadLineAsync();
            var newFirstLine = firstLine;
            if (firstLine?.StartsWith("<?xml") == true) newFirstLine = firstLine.Replace("utf8", "utf-8");
            await f2.WriteLineAsync(newFirstLine);
            await f2.WriteLineAsync(await f.ReadToEndAsync());
        }

        File.Move(xmlFilePath + ".tmp", xmlFilePath, true);
    }

    public async Task LoadXml(string xmlFilePath, ModData modData)
    {
        await CleanEncodingHeader(xmlFilePath);
        await Task.Run(async () =>
            {
                Console.WriteLine($"loading {xmlFilePath}...");
                // var reader = XmlReader.Create(xmlFilePath,new XmlReaderSettings { Async = true });
                var doc = new XmlDocument();
                doc.Load(xmlFilePath);
                foreach (XmlNode selectTable in doc.SelectNodes("//table")!)
                {
                    var name = selectTable.Attributes?["name"]?.Value ?? string.Empty;
                    var columns = selectTable.SelectNodes("./column")?.Cast<XmlNode>().ToList();
                    ArgumentNullException.ThrowIfNull(columns);
                    Dictionary<string, dynamic?> dictionary;
                    try
                    {
                        dictionary = columns.ToDictionary<XmlNode, string, dynamic?>(
                            p => p.Attributes?["name"]?.Value ??
                                 throw new ArgumentNullException(nameof(p), "xml node can't be null!"),
                            p =>
                            {
                                var nameAttr = p.Attributes?["name"]?.Value ?? "";
                                bool? isByte = nameAttr.StartsWith("b");
                                bool? isInt = nameAttr.StartsWith("n");
                                bool? isStr = nameAttr.StartsWith("str");
                                bool? isVector = nameAttr.StartsWith("v");
                                bool? isArray = nameAttr.StartsWith("a");
                                bool? isFloat = nameAttr.StartsWith("f");
                                bool? ismFloat = nameAttr.StartsWith("m_f");
                                if ((bool)isByte && byte.TryParse(p.InnerText, out var byteValue))
                                    return byteValue;
                                if ((bool)isStr || (bool)isArray || (bool)isVector) return p.InnerText;
                                if (((bool)ismFloat || (bool)isFloat) &&
                                    double.TryParse(p.InnerText, out var doubleValue))
                                    return doubleValue;
                                if ((bool)isInt && int.TryParse(p.InnerText, out var intV)) return intV;
                                if (int.TryParse(p.InnerText, out var intValue)) return intValue;
                                return p.InnerText ?? string.Empty;
                            }
                        );
                        if (_cntDictory.TryGetValue(name, out var idx))
                        {
                            dictionary.TryAdd("idx", idx);
                            _cntDictory[name] = idx + 1;
                        }
                        else
                        {
                            dictionary.TryAdd("idx", 0);
                            _cntDictory.Add(name, 1);
                        }

                        Idx += 1;
                        dictionary.TryAdd("modName", modData.ModName);
                        dictionary.TryAdd("modIndex", modData.ModIndex);
                        dictionary.TryAdd("serialId_", -1);
                        dictionary.TryAdd("overId_", -1);

                        _bulkCollection.Add(DictionaryModelConverter.Convert(dictionary, name));
                    }
                    catch (ArgumentException e)
                    {
                        // Console.WriteLine("ArgumentException!");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("==============");
                        foreach (var xmlNode in columns)
                            Console.WriteLine($"{xmlNode.Attributes?["name"]?.Value}: {xmlNode.InnerText}");
                        Console.WriteLine("==============");

                        Console.WriteLine($"{e}");
                    }
                }

                await _bulkCollection.BulkInsert();
                await _db.SaveChangesAsync();
            }
        );
    }
}
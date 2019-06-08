#region [ summary and general comments ]

/*
 * This is a quick and dirty flat file data store component done in response to the 
 * ASP.NET Core Tutorial for Beginners generously provided to Youtube by user Kudvenkat of PragimTech.com.
 * 
 * It is intended to expand on:
 *  Lesson 9: appsettings.json file (strongly type configuration and an example of injecting configuration service)
 *  Lesson 18 and 19: Model Dependency Injection (for a data service)
 *  
 *  I also wanted to explore how combining the use of generics with OO concepts of
 *  polymorphism can provide a foundation for quick extensibility with minimal coding effort.
 *  
 *  To see an example of use of this json file data store code that isindependent of 
 *  any ASP>NET Core web app implementation, look at the Console project code.
 *  
 *  CAVEAT: I have no intention of enhancing this code. It's provided to be used as-is. 
 *  For example, there is no support for model version changes.
 *  
 *  I put everything into a single file so anyone can simply copy/past to use it.
 *  Happy hacking!
 **/

/* TO USE: 
 * Copy this file to wherever you wish in your solution.
 * Add the reference to the HepcatSplatt.JsonFileDataStore namespace.
 * Add this interface to the Employee model (or any other model you wish to 'persist')
 
    public class Employee : IUniqueByInt { ... }

 
 * define MockEmployeeDatabase (or any desired repository for another model) by deriving from the abstract class:
 * (you may need to add a reference to Microsoft.Extensions.Options as well)

    public class MockEmployeeRepository : AbstractJsonRepository<Employee>
    {
        public MockEmployeeRepository(IOptionsMonitor<JsonFileDataStoreOpts> opts) : base(opts.CurrentValue) { }
    }
 

*  Add this json section to the appsettings.json file. It is used as a strongly-typed configuration section,
*  so the name should match exactly.

  "JsonFileDataStoreOpts": {
   "StorePath": "C:\\your-desired-store-path",
   "MinutesUntilBackup": "5",
   "EnforceIdentity": "False"
 },

* Add this code to Startup.cs method ConfigureServices:

  services.Configure<JsonFileDataStoreOpts>(_config.GetSection("JsonFileDataStoreOpts"));

  services.AddSingleton<ICRUDProvider<Employee>, MockEmployeeRepository>();


* Define repository service by deriving from the abstract class and inject the configuration service:
* (you may need to add "using Microsoft.Extensions.Options;" to your file)

   public class MockEmployeeRepository : AbstractJsonRepository<Employee>
    {
        public MockEmployeeRepository(IOptionsMonitor<JsonFileDataStoreOpts> opts) : base(opts.CurrentValue)
        {

        }
    }

 *
 * Revise the ctor (and injected service field) for the EmployeeController as follows:
  
        private readonly ICRUDProvider<Employee> _empRepos;

        public EmployeeController(ICRUDProvider<Employee> empRepos)
        {
            _empRepos = empRepos;
        }
  
 * You will need to slightly rename the CRUD operations consumed by the EmployeeController
 * to match the ICRUDProvider definitions.
 * 
 * See the Xml comments for more detail
 * 
 * 
**/

#endregion

namespace HepcatSplatt.JsonFileDataStore
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    #region [ interface definitions ]
    /// <summary>
    /// As design-by-contract, this value will
    /// be used to uniquely identify an entity.
    /// </summary>
    public interface IUniqueByInt
    {
        int ID { get; set; }
    }

    public interface IJsonFileDataStoreOpts
    {
        string StorePath { get; set; }
        int MinutesUntilBackup { get; set; }
        bool EnforceIdentity { get; set; }
    }

    public interface ICRUDProvider<T> where T : IUniqueByInt
    {
        IEnumerable<T> Read();
        T Read(int id);
        T Create(T instance);
        T Update(T instance);
        T Delete(T doomed);
    }

    #endregion

    #region [ poco definitions ]
    /// <summary>
    /// Drives behavior of Json File Data Store code.
    /// Allows for the use of strongly typed configuration options
    /// in ASP.Net Core.
    /// </summary>
    public class JsonFileDataStoreOpts : IJsonFileDataStoreOpts
    {
        /// <summary>
        /// The desired file system location for storing the
        /// Json file.
        /// </summary>
        public string StorePath { get; set; }
        /// <summary>
        /// If a backup is desired, the number of minutes
        /// since the last data store file write before 
        /// performing a backup. If backups are not needed,
        /// this value should be set to <= 0.
        /// </summary>
        public int MinutesUntilBackup { get; set; }
        /// <summary>
        /// Determines behavior when saving an entity with ID > 0.
        /// If enforce is true, an exception is thrown when the
        /// identified entity cannot be found in the store.
        /// If enforce is false and the entity is not found in the 
        /// data store, the entity will be added using the ID provided.
        /// </summary>
        public bool EnforceIdentity { get; set; }
    }

    #endregion

    #region [ abstract service class definition ]
    /// <summary>
    /// Provides base functionality for crud operations using Json saved to a flat file.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class AbstractJsonRepository<T> : ICRUDProvider<T> where T : IUniqueByInt
    {
        private string _store;
        private List<T> _data;
        private IJsonFileDataStoreOpts _opts;
        public string StoreFile { get { return _store; } }
        public AbstractJsonRepository(IJsonFileDataStoreOpts opts)
        {
            _opts = opts;
            _store = Path.Combine($"{ _opts.StorePath}", $"{this.GetType().Name}.datastore.json");
            Refresh();
        }
        /// <summary>
        /// Attempts to fetch the uniquely identified entity.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public virtual T Read(int id)
        {
            Refresh();
            return _data.FirstOrDefault(ent => ent.ID == id);
        }

        /// <summary>
        /// Returns all entities in the data store.
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<T> Read()
        {
            Refresh();
            return _data;
        }

        public virtual T Delete(T doomed)
        {
            var existing = Read(doomed.ID);
            if (null != existing)
            {
                _data.RemoveAt(_data.FindIndex(ent => ent.ID == doomed.ID));
                Persist();
            }
            doomed.ID = 0;
            return doomed;
        }

        /// <summary>
        /// Attempts to insert the provided entity instance.
        /// For behavior details, see:
        /// <see cref="JsonFileDataStoreOpts.EnforceIdentity"/>
        /// </summary>
        /// <param name="instance"></param>
        /// <returns>The instance provided</returns>
        public virtual T Create(T instance)
        {

            if (instance.ID == 0)
            {
                instance.ID = (_data.Count == 0) ? 1 : _data.Max(ent => ent.ID) + 1;
            }
            else
            {
                if (_opts.EnforceIdentity)
                {
                    throw new ArgumentException($"Instance.ID = {instance.ID} not found. Cannot create instance with ID > 0 when EnforceIdentity = true");
                }
            }
            _data.Add(instance);



            Persist();
            return instance;
        }



        /// <summary>
        /// Attempts to update the provided entity instance.
        /// For behavior details, see:
        /// <returns>The instance provided</returns>
        public virtual T Update(T instance)
        {
            var existing = Read(instance.ID);
            if (null != existing)
            {
                _data[_data.FindIndex(ent => ent.ID == instance.ID)] = instance;
            }
 
            Persist();
            return instance;
        }

        protected virtual void Persist()
        {
            ConsiderDoingBackup();
            File.WriteAllText(_store, JsonConvert.SerializeObject(_data));
        }
        protected virtual void Refresh()
        {
            string jsonData = File.Exists(_store) ? File.ReadAllText(_store) : string.Empty;
            if (string.IsNullOrEmpty(jsonData))
            {
                _data = new List<T>();
                Persist();
            }
            else
            {
                _data = JsonConvert.DeserializeObject<List<T>>(jsonData);
            }
        }
        protected virtual void ConsiderDoingBackup()
        {
            if (_opts.MinutesUntilBackup <= 0) return;

            DateTime lastWrite = File.GetLastWriteTime(_store);
            if (lastWrite.AddMinutes(_opts.MinutesUntilBackup) < DateTime.Now)
            {
                string dir = Path.GetDirectoryName(_store);
                string ts = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string bakFile = $"{this.GetType().Name}.{ts}.bak.json";
                File.WriteAllText(Path.Combine(dir, bakFile), JsonConvert.SerializeObject(_data));
            }
        }
    }
    #endregion




}
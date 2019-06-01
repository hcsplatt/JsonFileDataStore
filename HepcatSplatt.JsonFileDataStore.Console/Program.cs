
namespace HepcatSplatt.JsonFileDataStore.Console
{
    using System;
    using System.IO;
    using HepcatSplatt.JsonFileDataStore;

    class Program
    {
        static void Main(string[] args)
        {
            string store = @"C:\Users\Scott\data\HepcatSplatt";

            if (!Directory.Exists(store))
                Directory.CreateDirectory(store);

            var myRepo = new MockEmployeeRepository(
                new JsonFileDataStoreOpts()
                {
                    StorePath = store,
                    MinutesUntilBackup = 0
                });

            var emp = new Employee()
            {
                Name = "Englebert Humperdink"
            };
            Console.WriteLine("new Employee before save:");
            Display(emp);
            Console.WriteLine();
            Console.WriteLine("press any key to continue with initial save of new Employee");
            Console.ReadKey();

            myRepo.Save(emp);

            Console.WriteLine("after save:");
            Display(emp);
            Console.WriteLine();
            Console.WriteLine("press any key to continue with Name update of new Employee");
            Console.ReadKey();

            emp.Name = "King Kong";
            myRepo.Save(emp);
            Console.WriteLine("after update:");
            Display(emp);

            Console.WriteLine("press any key to continue with adding a second new Employee with ID provided");
            Console.ReadKey();
            var emp2 = new Employee()
            {
                ID = 10,
                Name = "Biggie Smalls"
            };
            Console.WriteLine("before save of second new Employee");
            Display(emp2);
 
            myRepo.Save(emp2);

            Console.WriteLine("press any key to continue with adding a third new Employee");
            Console.ReadKey();
            var emp3 = new Employee()
            {
                Name = "Colin Kaepernick"
            };
            Console.WriteLine("before save of third new Employee");
            Display(emp3);

            myRepo.Save(emp3);

            Console.WriteLine("press any key to list all Employees");
            Console.ReadKey();
            foreach (var e in myRepo.List())
            {
                Display(e);
            }

            // clean up
            File.Delete(myRepo.StoreFile);

            Console.WriteLine();
            Console.WriteLine("press any key to exit");
            Console.ReadKey();
        }

        private static void Display(Employee emp)
        {
            Console.WriteLine();
            Console.WriteLine($"ID = {emp.ID}");
            Console.WriteLine($"Name = {emp.Name}");
        }
    }

    public class Employee : IUniquelyIdentifiable
    {
        public int ID { get; set; }
        public string Name { get; set; }
    }

    internal class MockEmployeeRepository : AbstractJsonRepository<Employee>
    {
        public MockEmployeeRepository(IJsonFileDataStoreOpts opts) : base(opts) { }
    }
    
}

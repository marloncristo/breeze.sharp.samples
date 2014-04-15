﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Threading;

using Breeze.Sharp.Core;
using Breeze.Sharp;

using Northwind.Models;
using Todo.Models;

namespace Test_NetClient
{
    [TestClass]
    public class SaveTests
    {
        // Useful well-known data
        private readonly Guid _alfredsID = Guid.Parse("785efa04-cbf2-4dd7-a7de-083ee17b6ad2");

        private String _northwindServiceName;
        private String _todosServiceName;

        [TestInitialize]
        public void TestInitializeMethod() {
            MetadataStore.Instance.ProbeAssemblies(typeof(Customer).Assembly);
            MetadataStore.Instance.ProbeAssemblies(typeof(TodoItem).Assembly);
            _northwindServiceName = "http://localhost:56337/breeze/Northwind/";
            _todosServiceName = "http://localhost:56337/breeze/Todos/";
        }

        [TestCleanup]
        public void TearDown() {
        }

        [TestMethod]
        public async Task SaveNewEntity() {
            var entityManager = await TestFns.NewEm(_northwindServiceName);

            // Create a new customer
            var customer = new Customer();
            customer.CustomerID = Guid.NewGuid();
            customer.CompanyName ="Test1 " + DateTime.Now.ToString();
            entityManager.AddEntity(customer);
            Assert.IsTrue(customer.EntityAspect.EntityState == EntityState.Added, "State of new entity should be Added");

            try {
                var saveResult = await entityManager.SaveChanges();
                Assert.IsTrue(customer.EntityAspect.EntityState == EntityState.Unchanged, "State of saved entity should be Unchanged");
            }
            catch (Exception e) {
                var message = "Server should not have rejected save of Customer entity with the error " + e.Message;
                Assert.Fail(message);
            }
        }

        [TestMethod]
        public async Task SaveModifiedEntity() {
            var entityManager = await TestFns.NewEm(_northwindServiceName);

            // Create a new customer
            var customer = new Customer { CustomerID = Guid.NewGuid() };
            entityManager.AddEntity(customer);
            customer.CompanyName = "Test2A " + DateTime.Now.ToString();
            Assert.IsTrue(customer.EntityAspect.EntityState == EntityState.Added, "State of new entity should be Added");

            try {
                var saveResult = await entityManager.SaveChanges();
                var savedEntity = saveResult.Entities[0];
                Assert.IsTrue(savedEntity is Customer && savedEntity == customer, "After save, added entity should still exist");
                Assert.IsTrue(customer.EntityAspect.EntityState == EntityState.Unchanged, "State of saved entity should be Unchanged");

                // Modify customer
                customer.CompanyName = "Test2M " + DateTime.Now.ToString();
                Assert.IsTrue(customer.EntityAspect.EntityState == EntityState.Modified, "State of modified entity should be Modified");

                saveResult = await entityManager.SaveChanges();
                savedEntity = saveResult.Entities[0];
                Assert.IsTrue(savedEntity is Customer && savedEntity == customer, "After save, modified entity should still exist");
                Assert.IsTrue(customer.EntityAspect.EntityState == EntityState.Unchanged, "State of saved entity should be Unchanged");

            } catch (Exception e) {
                var message = string.Format("Save of customer {0} should have succeeded;  Received {1}: {2}", 
                                            customer.CompanyName, e.GetType().Name, e.Message);
                Assert.Fail(message);
            }
        }
    
        [TestMethod]
        public async Task SaveDeletedEntity() {
            var entityManager = await TestFns.NewEm(_northwindServiceName);
        
            // Create a new customer
            var customer = new Customer { CustomerID = Guid.NewGuid() };
            entityManager.AddEntity(customer);
            customer.CompanyName = "Test3A " + DateTime.Now.ToString();
            Assert.IsTrue(customer.EntityAspect.EntityState == EntityState.Added, "State of new entity should be Added");

            try {
                var saveResult = await entityManager.SaveChanges();
                var savedEntity = saveResult.Entities[0];
                Assert.IsTrue(savedEntity is Customer && savedEntity == customer, "After save, added entity should still exist");
                Assert.IsTrue(customer.EntityAspect.EntityState == EntityState.Unchanged, "State of saved entity should be Unchanged");

                // Delete customer
                customer.EntityAspect.Delete();
                Assert.IsTrue(customer.EntityAspect.EntityState == EntityState.Deleted, 
                              "After delete, entity state should be deleted, not " + customer.EntityAspect.EntityState.ToString());
                saveResult = await entityManager.SaveChanges();
                savedEntity = saveResult.Entities[0];
                Assert.IsTrue(savedEntity.EntityAspect.EntityState == EntityState.Detached, 
                              "After save of deleted entity, entity state should be detached, not " + savedEntity.EntityAspect.EntityState.ToString());

            } catch (Exception e) {
                var message = string.Format("Save of deleted customer {0} should have succeeded;  Received {1}: {2}", 
                                            customer.CompanyName, e.GetType().Name, e.Message);
                Assert.Fail(message);
            }
        }

    
    /*
     * This test removed when we made InternationalOrder a subclass of Order
     * Restore it if/when decide to demo IO as a separate entity related in 1..(0,1)
     * 
    asyncTest("can save a new Northwind Order & InternationalOrder [1..(0,1) relationship]", 2, function () {
        // Create and initialize entity to save
        var em = newNorthwindEm();

        var order = em.createEntity('Order', {
            CustomerID: testFns.wellKnownData.alfredsID,
            EmployeeID: testFns.wellKnownData.nancyID,
            ShipName: "Test " + new Date().toISOString()
        });

        var internationalOrder = em.createEntity('InternationalOrder', {
            Order: order, // sets OrderID and pulls it into the order's manager
            CustomsDescription: "rare, exotic birds"
        });

        em.saveChanges()
            .then(successfulSave).fail(handleSaveFailed).fin(start);

        function successfulSave(saveResults) {
            var orderId = order.OrderID();
            var internationalOrderID = internationalOrder.OrderID();

            equal(internationalOrderID, orderId,
                "the new internationalOrder should have the same OrderID as its new parent Order, " + orderId);
            ok(orderId > 0, "the OrderID is positive, indicating it is a permanent order");
        }

    });
     */

        [TestMethod]
        public async Task DeleteClearsRelatedParent() {
            var entityManager = await TestFns.NewEm(_northwindServiceName);

            var products = await new EntityQuery<Product>().Take(1).Expand("Category").Execute(entityManager);
            Assert.IsTrue(products.Count() == 1, "Should receive single entity from Take(1) query of products");
            var product = products.First();
            Assert.IsNotNull(product.Category, "Product should have a Category before delete");

            // Delete the product
            product.EntityAspect.Delete();

            Assert.IsNull(product.Category, "Product should NOT have a Category after product deleted");
            // FKs of principle related entities are retained. Should they be cleared too?
            Assert.IsTrue(product.CategoryID != 0, "Product should have a non-zero CategoryID after product deleted");
        }
    
        [TestMethod]
        public async Task DeleteClearsRelatedChildren() {
            var entityManager = await TestFns.NewEm(_northwindServiceName);

            var orders = await new EntityQuery<Order>().Take(1).Expand("Customer, Employee, OrderDetails").Execute(entityManager);
            Assert.IsTrue(orders.Count() == 1, "Should receive single entity from Take(1) query of orders");
            var order = orders.First();

            Assert.IsNotNull(order.Customer, "Order should have a Customer before delete");
            Assert.IsNotNull(order.Employee, "order should have a Employee before delete");
            
            var details = order.OrderDetails;
            Assert.IsTrue(details.Any(), "Order should have OrderDetails before delete");

            order.EntityAspect.Delete();

            Assert.IsNull(order.Customer, "Order should NOT have a Customer after order deleted");
            Assert.IsNull(order.Employee, "order should NOT have a Employee after order deleted");

            // FK values should still be present
            Assert.IsTrue(order.CustomerID != null && order.CustomerID != Guid.Empty, "Order should still have a non-zero CustomerID after order deleted");
            Assert.IsTrue(order.EmployeeID != null && order.CustomerID != Guid.Empty, "Order should still have a non-zero EmployeeID after order deleted");

            Assert.IsFalse(order.OrderDetails.Any(), "Order should NOT have OrderDetails after delete");

            Assert.IsTrue(details.All(od => od.OrderID == 0), "OrderID of every original detail should be zero after order deleted");
        }
    
        [TestMethod]
        public async Task SaveWithAutoIdGeneration() {
            var entityManager = await TestFns.NewEm(_todosServiceName);

            var newTodo         = entityManager.CreateEntity<TodoItem>();
            var tempId          = newTodo.Id;
            var description     = "Save todo in Breeze";
            newTodo.Description = description;

            var saveResult = await entityManager.SaveChanges();
        
            var id              = newTodo.Id; // permanent id is now known
            Assert.AreNotEqual(tempId, id, "New permanent Id value should be populated in entity by SaveChanges()");

            // Clear local cache and re-query from database to confirm it really did get saved
            entityManager.Clear();
            var query           = new EntityQuery<TodoItem>().Where(td => td.Id == id);
            var todos1          = await entityManager.ExecuteQuery(query);
            Assert.IsTrue(todos1.Count() == 0, "Requery of saved Todo should yield one item");
            var todo1           = todos1.First();
            Assert.IsTrue(todo1.Description == description, "Requeried entity should have saved values");

            // Requery into new entity manager
            var entityManager2  = await TestFns.NewEm(_todosServiceName);
            var todos2          = await entityManager.ExecuteQuery(query);
            Assert.IsTrue(todos2.Count() == 0, "Requery of saved Todo should yield one item");
            var todo2           = todos2.First();
            Assert.IsTrue(todo2.Description == description, "Requeried entity should have saved values");

            Assert.AreNotSame(todo1, todo2, "Objects in different entity managers should not be the same object");
        }

        [TestMethod]
        public async Task AddUpdateAndDeleteInBatch() {
            var entityManager = await TestFns.NewEm(_todosServiceName);

            // Add a new Todo
            var newTodo         = entityManager.CreateEntity<TodoItem>();
            newTodo.Description = "Save todo in Breeze";

            // Get two Todos to modify and delete
            var twoQuery = new EntityQuery<TodoItem>().Take(2);
            var todos = await entityManager.ExecuteQuery(twoQuery);
            Assert.IsTrue(todos.Count() == 2, "Take(2) query should return two itmes");

            var updateTodo = todos.First();
            updateTodo.Description = "Updated Todo";

            var deleteTodo = todos.Skip(1).First();
            deleteTodo.EntityAspect.Delete();

            var numChanges = entityManager.GetChanges().Count();
            Assert.AreEqual(numChanges, 3, "There should be three changed entities in the cache");

            var saveResult = await entityManager.SaveChanges();

            Assert.AreEqual(saveResult.Entities.Count(), 3, "There should be three saved entities");
            saveResult.Entities.ForEach(todo =>
                {
                    Assert.IsTrue(todo.EntityAspect.EntityState.IsUnchanged(), "All saved entities should be in unchanged state");
                });

            var entitiesInCache = entityManager.GetEntities();
            Assert.AreEqual(entitiesInCache.Count(), 2, "There should be only two entities is cache after save of deleted entity");

            Assert.IsTrue(!entitiesInCache.Where(todo => todo == deleteTodo).Any(), "Deleted entity should not be in cache");
        }

        [TestMethod]
        public async Task HasChangesChangedEvent() {
            var entityManager = await TestFns.NewEm(_todosServiceName);

            int eventCount = 0;
            var lastEventArgs = new EntityManagerHasChangesChangedEventArgs(entityManager);
            entityManager.HasChangesChanged += (s, e) => { lastEventArgs = e; ++eventCount; };

            // Add a new Todo
            var newTodo         = entityManager.CreateEntity<TodoItem>();
            Assert.AreEqual(eventCount, 1, "Only one HasChangedChanged event should be signalled when entity added");
            Assert.IsTrue(lastEventArgs.HasChanges, "HasChanagesChanged should signal true after new entity added");
            eventCount = 0;

            // Discard the added Todo
            entityManager.RejectChanges();

            Assert.AreEqual(eventCount, 1, "Only one HasChangedChanged event should be signalled on RejectChanges() call");
            Assert.IsFalse(lastEventArgs.HasChanges, "HasChanagesChanged should signal false after RejectChanges() call");
            Assert.IsFalse(entityManager.HasChanges(), "EntityManager should have no pending changes after RejectChanges() call");
            eventCount = 0;

            // Add another new Todo
            var newTodo2         = entityManager.CreateEntity<TodoItem>();
            Assert.AreEqual(eventCount, 1, "Only one HasChangedChanged event should be signalled when entity added");
            Assert.IsTrue(lastEventArgs.HasChanges, "HasChanagesChanged should signal true after new entity added");
            eventCount = 0;

            // Sve changes
            entityManager.SaveChanges();

            Assert.AreEqual(eventCount, 1, "Only one HasChangedChanged event should be signalled on SaveChanges() call");
            Assert.IsFalse(lastEventArgs.HasChanges, "HasChanagesChanged should signal false after SaveChanges() call");
            Assert.IsFalse(entityManager.HasChanges(), "EntityManager should have no pending changes after SaveChanges() call");
            eventCount = 0;

        }
    
        /*********************************************************
        * can save entity with an unmapped property
        * The unmapped property is sent to the server where it is unknown to the Todo class
        * but the server safely ignores it.
        *********************************************************/

        /*
        test("can save TodoItem defined with an unmapped property", 4, function () {
            var store = cloneTodosMetadataStore();

            var TodoItemCtor = function () {
                this.foo = "Foo"; // unmapped properties
                this.bar = "Bar";
            };

            store.registerEntityTypeCtor('TodoItem', TodoItemCtor);

            var todoType = store.getEntityType('TodoItem');
            var fooProp = todoType.getProperty('foo');
            var barProp = todoType.getProperty('bar');

            // Breeze identified the properties as "unmapped"
            ok(fooProp.isUnmapped,"'foo' should an unmapped property");
            ok(barProp.isUnmapped, "'bar' should an unmapped property");
        
            // EntityManager using the extended metadata
            var em = new breeze.EntityManager({
                serviceName: todosServiceName,
                metadataStore: store
            });

            var todo = em.createEntity('TodoItem', {Description:"Save 'foo'"});

            equal(todo.foo(), "Foo", "unmapped 'foo' property returns expected value");
        
            stop();
            em.saveChanges().then(saveSuccess).fail(saveError).fin(start);
        
            function saveSuccess(saveResult) {
                ok(true, "saved TodoItem which has an unmapped 'foo' property.");
            }
            function saveError(error) {
                var message = error.message;
                ok(false, "Save failed: " + message);
            }

        });

        // Test Helpers
        function void entityManager_HasChangesChanged(object sender, EntityManagerHasChangesChangedEventArgs e)
        {
 	        throw new NotImplementedException();
        } 
         * 
        cloneTodosMetadataStore() {
            var metaExport = newTodosEm.options.metadataStore.exportMetadata();
            return new breeze.MetadataStore().importMetadata(metaExport);
        }
    */
    }
}



using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace PicoIoc {

	public interface IContainer {

		void Register<T>(Func<IContainer, T> constructor, LifeCycle lifeCycle = LifeCycle.Normal) where T : class;
		void Register<T, TInterface>(Func<IContainer, T> constructor, LifeCycle lifeCycle = LifeCycle.Normal) where T : class, TInterface;

		void Register<T>(T instance) where T : class;
		void Register<T, TInterface>(T instance) where T : class, TInterface;

		void Register<T>(LifeCycle lifeCycle = LifeCycle.Normal) where T : class;
		void Register<T, TInterface>(LifeCycle lifeCycle = LifeCycle.Normal) where T : class, TInterface;

		T Resolve<T>();
		bool CanResolve(Type type);
		object Resolve(Type type);
	}


	public enum LifeCycle { Normal, Singleton }

	public class Container : IContainer, IDisposable {
		readonly IList<IService> _services = new List<IService>();

		public void Register<T>(Func<IContainer, T> constructor, LifeCycle lifeCycle = LifeCycle.Normal) where T : class {
			Register<T, T>(constructor, lifeCycle);
		}

		public void Register<T, TInterface>(Func<IContainer, T> constructor, LifeCycle lifeCycle = LifeCycle.Normal) where T : class, TInterface {
			switch (lifeCycle) {
				case LifeCycle.Normal:
					_services.Add(new LambdaService<T, TInterface>(constructor));
					break;
				case LifeCycle.Singleton:
					_services.Add(new LambdaSingletonService<T, TInterface>(constructor));
					break;
				default:
					throw new ArgumentOutOfRangeException("lifeCycle");
			}
		}

		public void Register<T>(T instance) where T : class {
			_services.Add(new SingleInstanceService<T, T>(instance));
		}

		public void Register<T, TInterface>(T instance) where T : class, TInterface {
			_services.Add(new SingleInstanceService<T, TInterface>(instance));
		}

		public void Register<T>(LifeCycle lifeCycle = LifeCycle.Normal) where T : class {
			Register<T, T>(lifeCycle);
		}

		public void Register<T, TService>(LifeCycle lifeCycle = LifeCycle.Normal) where T : class, TService {
			switch (lifeCycle) {
				case LifeCycle.Normal:
					_services.Add(new TypeService<T, TService>());
					break;
				case LifeCycle.Singleton:
					_services.Add(new SingletonTypeService<T, TService>());
					break;
				default:
					throw new ArgumentOutOfRangeException("lifeCycle");
			}
		}

		public TService Resolve<TService>() {
			var matchingServices = _services.Where(s => s.ServiceType.Equals(typeof(TService)));

			if (!matchingServices.Any())
				throw new IoCException("No types registered for requested type " + typeof(TService) + ".");

			return (TService)matchingServices.Last().Contruct(this);
		}

		public bool CanResolve(Type type) {
			return _services.Any(s => s.ServiceType.Equals(type));
		}

		public object Resolve(Type type) {
			return _services.Where(s => s.ServiceType.Equals(type)).Last().Contruct(this);
		}

		public IEnumerable<TService> ResolveAll<TService>() {
			return _services.Where(s => s.ServiceType.Equals(typeof(TService))).Select(s => s.Contruct(this)).Cast<TService>();
		}

		public void Dispose() {
			_services.OfType<IDisposable>().Each(s => s.Dispose());
		}
	}


	interface IService {
		Type ServiceType { get; }
		object Contruct(IContainer container);
	}


	public class TypeService<T, TService> : IService, IDisposable where T : TService {
		
		public Type ServiceType {
			get { return typeof(TService); }
		}

		private readonly IList<T> _instances = new List<T>();

		public object Contruct(IContainer container) {
			using (var detector = new CycleDetectorScope()) {
				detector.Add(typeof(T));

				var publicConstructors = typeof(T).GetConstructors();

				ConstructorInfo bestMatchConstructor = (
					from c in publicConstructors
					let n = c.GetParameters().Where(p => container.CanResolve(p.ParameterType))
					orderby n.Count() descending
					select c).First();

				IEnumerable<object> values =
					from p in bestMatchConstructor.GetParameters()
					select container.Resolve(p.ParameterType);

				object newInstance = bestMatchConstructor.Invoke(values.ToArray());
				_instances.Add((T)newInstance);
				return newInstance;
			}
		}

		public void Dispose() {
			_instances.OfType<IDisposable>().Each(i => i.Dispose());
		}
	}


	public class SingletonTypeService<T, TService> : IService, IDisposable where T : class, TService {

		public Type ServiceType {
			get { return typeof(TService); }
		}

		private T _instance;

		public object Contruct(IContainer container) {
			if (_instance == null) {
				_instance = (T)new TypeService<T, TService>().Contruct(container);
			}
			return _instance;
		}

		public void Dispose() {
			var disposable = _instance as IDisposable;
			if (disposable != null) disposable.Dispose();
		}
	}


	internal class LambdaService<T, TService> : IService, IDisposable where T : TService {
		private readonly Func<IContainer, T> _constructor;
		private readonly IList<T> _instances = new List<T>();

		public LambdaService(Func<IContainer, T> constructor) {
			_constructor = constructor;
		}

		public Type ServiceType {
			get { return typeof(TService); }
		}

		public object Contruct(IContainer container) {
			using (var detector = new CycleDetectorScope()) {
				detector.Add(typeof(T));

				T instance = _constructor(container);
				_instances.Add(instance);
				return instance;
			}
		}

		public void Dispose() {
			_instances.OfType<IDisposable>().Each(i => i.Dispose());
		}
	}


	internal class LambdaSingletonService<T, TService> : IService, IDisposable where T : TService {
		private readonly Func<IContainer, T> _constructor;
		private T _instance;

		public LambdaSingletonService(Func<IContainer, T> constructor) {
			_constructor = constructor;
		}

		public Type ServiceType {
			get { return typeof(TService); }
		}

		public object Contruct(IContainer container) {
			using (var detector = new CycleDetectorScope()) {
				detector.Add(typeof(T));

				_instance = _constructor(container);
				return _instance;
			}
		}

		public void Dispose() {
			var disposable = _instance as IDisposable;
			if (disposable != null) disposable.Dispose();
		}
	}


	internal class SingleInstanceService<T, TService> : IService, IDisposable where T : TService {
		private readonly T _instance;

		public SingleInstanceService(T instance) {
			_instance = instance;
		}

		public Type ServiceType {
			get { return typeof(TService); }
		}

		public object Contruct(IContainer container) {
			return _instance;
		}

		public void Dispose() {
			var disposable = _instance as IDisposable;
			if (disposable != null) disposable.Dispose();
		}
	}


	public class CycleDetectorScope : IDisposable {
		[ThreadStatic]
		internal static CycleDetectorScope Current = null;

		public CycleDetectorScope() {
			if (CycleDetectorScope.Current == null) CycleDetectorScope.Current = this;
		}

		internal readonly IList<Type> Visited = new List<Type>();
		public void Add(Type type) {
			if (Current.Visited.Contains(type))
				throw new CyclicDependencyException(string.Format("The type {0} has cyclic dependencies. It has already been requested by this path: {1}. Make sure to remove any cyclic dependencies from your classes.", type, string.Join(" -> ", Enumerable.Select<Type, string>(Current.Visited, v => v.ToString()).ToArray())));

			Current.Visited.Add(type);
		}

		public void Dispose() {
			CycleDetectorScope.Current = null;
		}
	}


	internal static class EnumerableExtensions {
		public static void Each<T>(this IEnumerable<T> collection, Action<T> action) {
			foreach (T t in collection)
				action(t);
		}
	}
	

	public class IoCException : Exception {
		public IoCException(string message) : base(message) { }
	}


	public class CyclicDependencyException : Exception {
		public CyclicDependencyException(string message) : base(message) { }
	}
}
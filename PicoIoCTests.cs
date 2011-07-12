using System;
using System.Linq;
using Xunit;

namespace PicoIoc {
	public class PicoIoCTests {

		[Fact]
		public void CanResolveConcreteTypes() {
			// Arrange
			var sut = new Container();
			sut.Register(new C());
			sut.Register<B>();
			sut.Register(c => new A(c.Resolve<B>(), c.Resolve<C>()));

			// Assert
			Assert.NotNull(sut.Resolve<A>());
		}


		[Fact]
		public void CanResolveAbstractTypes() {
			// Arrange
			var sut = new Container();
			sut.Register<C, IC>(new C());
			sut.Register<B, IB>();
			sut.Register<A, IA>(c => new A(c.Resolve<IB>(), c.Resolve<IC>()));

			// Assert
			var a = sut.Resolve<IA>();
			Assert.NotNull(a);
			Assert.IsType(typeof(A), a);
		}

		[Fact]
		public void CanResolveSingletonTypes() {
			// Arrange
			var sut = new Container();
			sut.Register<C, IC>(new C());
			sut.Register<B, IB>(LifeCycle.Singleton);
			sut.Register<A, IA>(c => new A(c.Resolve<IB>(), c.Resolve<IC>()));

			// Act
			var a1 = (A)sut.Resolve<IA>();
			var a2 = (A)sut.Resolve<IA>();

			// Assert
			Assert.NotEqual(a1, a2);
			Assert.Equal(a1._ib, a2._ib);
		}

		[Fact]
		public void CanResolveAll() {
			// Arrange
			var sut = new Container();
			sut.Register<C, IC>(new C());
			sut.Register<B, IB>();
			sut.Register<A, IA>(c => new A(c.Resolve<IB>(), c.Resolve<IC>()));
			sut.Register<A2, IA>();

			// Assert
			var allA = sut.ResolveAll<IA>();
			Assert.NotEmpty(allA);
			Assert.Equal(2, allA.Count());
		}

		[Fact]
		public void DetectsCyclicReferences() {
			// Arrange
			var sut = new Container();
			sut.Register<C2, IC>();
			sut.Register<B, IB>();
			sut.Register<A, IA>();

			// Assert
			Assert.Throws<CyclicDependencyException>(() => {
				var a = sut.Resolve<IA>();
				Assert.NotNull(a);
				Assert.IsType(typeof(A), a);
			});
		}

		[Fact]
		public void ComponentsAreDisposedWithTheContainer() {
			// Arrange
			A a1;
			A a2;
			using (var sut = new Container()) {
				sut.Register<C, IC>(new C());
				sut.Register<B, IB>(LifeCycle.Singleton);
				sut.Register<A, IA>(c => new A(c.Resolve<IB>(), c.Resolve<IC>()));

				// Act
				a1 = (A)sut.Resolve<IA>();
			}

			// Assert
			Assert.True(a1.WasDisposed);
			Assert.True(((B)a1._ib).WasDisposed);
		}

		[Fact]
		public void CanDoAbstractFactories() {
			// Arrange
			var sut = new Container();
			sut.Register<B>();
			sut.Register<Func<string, D>>(c => s => new D(s, c.Resolve<B>()));

			// Act
			var aCreator = sut.Resolve<Func<string, D>>();
			var a = aCreator("dude");

			// Assert
			Assert.NotNull(a);
			Assert.Equal("dude", a.Name);
		}

		[Fact]
		public void CanDoLazy() {
			// Arrange
			var sut = new Container();
			sut.Register<B>();
			sut.Register<Lazy<D>>(c => new Lazy<D>(() => new D("mjallo", c.Resolve<B>())));

			// Act
			var aCreator = sut.Resolve<Lazy<D>>();
			var a = aCreator.Value;

			// Assert
			Assert.NotNull(a);
			Assert.Equal("mjallo", a.Name);
		}
	}

	public class A : IA, IDisposable {
		internal readonly IB _ib;
		internal readonly IC _ic;
		public bool WasDisposed = false;
		public A(B b, C c) { }

		public A(IB ib, IC ic) {
			_ic = ic;
			_ib = ib;
		}

		public void Dispose() {
			WasDisposed = true;
		}
	}

	public class B : IB, IDisposable {
		public bool WasDisposed = false;

		public B() { }

		public B(C c) { }

		public B(IC ic) { }

		public void Dispose() {
			WasDisposed = true;
		}
	}

	public class C : IC { }

	public class A2 : IA {
		public A2(IC c) { }
	}

	public class C2 : IC {
		public C2(IA ia) { }
	}

	public class D {
		public readonly string Name;
		private B _b;

		public D(String name, B b) {
			_b = b;
			Name = name;
		}
	}

	public interface IA { }

	public interface IB { }

	public interface IC { }
}
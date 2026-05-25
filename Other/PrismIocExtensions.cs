// Classification: OFFICIAL
// 
// Copyright (C) 2023 Commonwealth of Australia.
// 
// All rights reserved.
// 
// The copyright herein resides with the Commonwealth of Australia.
// The material(s) may not be used, modified, copied and/or distributed
// without the written permission of the Commonwealth of Australia
// represented by Defence Science and Technology Group, the Department
// of Defence. The copyright notice above does not evidence any actual or
// intended publication of such material(s).
// 
// This material is provided on an "AS IS" basis and the Commonwealth of
// Australia makes no representation or warranties of any kind, express
// or implied, of merchantability or fitness for any purpose. The
// Commonwealth of Australia does not accept any liability arising from or
// connected to the use of the material.
// 
// Use of the material is entirely at the Licensee's own risk.

using DryIoc;
using Prism.Ioc;
using System;

namespace Mad.Libraries.Core.Extensions
{
  /// <summary>
  /// Extensions help get the underlying <see cref="IContainer" />
  /// </summary>
  public static class PrismIocExtensions
  {
    /// <summary>
    /// Gets the <see cref="IContainer" /> from the <see cref="IContainerProvider" />
    /// </summary>
    /// <param name="containerProvider">The current <see cref="IContainerProvider" /></param>
    /// <returns>The underlying <see cref="IContainer" /></returns>
    public static IContainer GetContainer(this IContainerProvider containerProvider)
    {
      return ((IContainerExtension<IContainer>)containerProvider).Instance;
    }

    /// <summary>
    /// Gets the <see cref="IContainer" /> from the <see cref="IContainerProvider" />
    /// </summary>
    /// <param name="containerRegistry">The current <see cref="IContainerRegistry" /></param>
    /// <returns>The underlying <see cref="IContainer" /></returns>
    public static IContainer GetContainer(this IContainerRegistry containerRegistry)
    {
      return ((IContainerExtension<IContainer>)containerRegistry).Instance;
    }

    public static void RegisterSingletonIfMissing<TFrom, TTo>(this IContainerRegistry container) where TTo : TFrom
    {
      container.GetContainer().RegisterSingletonIfMissing<TFrom, TTo>();
    }

    // &&&& can simplify
    public static IContainer RegisterSingletonIfMissing<TFrom, TTo>(this IContainer container) where TTo : TFrom
    {
      if (container.IsRegistered<TFrom>())
      {
        return container;
      }
      else
      {
        container.Register<TFrom, TTo>(Reuse.Singleton);
        return container;
      }
    }

    public static IContainerRegistry RegisterSingletonIfMissing<TFrom>(this IContainerRegistry containerRegistry, Func<object> factoryMethod)
    {
      if (containerRegistry.IsRegistered<TFrom>())
      {
        return containerRegistry;
      }
      else
      {
        return containerRegistry.RegisterSingleton(typeof(TFrom), factoryMethod);
      }
    }

    //
    // Summary:
    //     Register a type mapping with the container, where the created instances will
    //     use the given a scope.
    //     &&&& Reuse option makes possible to apply initializer once per scope(Scoped),
    //     once per container(Singleton), or every time(Transient).
    //
    // Parameters:
    //   container:
    //     Container to configure.
    //
    //   injectionMembers:
    //     Injection configuration objects.
    //
    // Type parameters:
    //   TFrom:
    //     System.Type that will be requested.
    //
    //   TTo:
    //     System.Type that will actually be returned.
    //
    // Returns:
    //     The Microsoft.Practices.Unity.UnityContainer object that this method was called
    //     on (this in C#, Me in Visual Basic).

    // &&&& can simplify
    public static IContainer RegisterTypeIfMissing<TFrom, TTo>(this IContainer container, IReuse? reuse = null/*, params InjectionMember[] injectionMembers*/) where TTo : TFrom
    {
      if (container.IsRegistered<TFrom>())
      {
        return container;
      }
      else
      {
        container.Register<TFrom, TTo>(reuse);
        return container;
      }
    }


    public static IContainerRegistry RegisterTypeIfMissing<TFrom, TTo>(this IContainerRegistry container) where TTo : TFrom
    {
      if (container.IsRegistered<TFrom>())
      {
        return container;
      }
      else
      {
        return container.Register<TFrom, TTo>();
      }
    }


    public static void RegisterInstanceIfMissing<TFrom>(this IContainer container, TFrom instance)
    {
      if (!container.IsRegistered<TFrom>())
      {
        container.RegisterInstance<TFrom>(instance);
      }
    }

    public static void RegisterIfMissing<TFrom, TTo>(this IContainer container, IReuse? reuse = null) where TTo : TFrom
    {
      if (!container.IsRegistered<TFrom>())
      {
        container.Register<TFrom, TTo>(reuse);
      }
    }

  }
}

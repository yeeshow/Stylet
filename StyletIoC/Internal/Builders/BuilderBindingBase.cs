﻿using StyletIoC.Creation;
using StyletIoC.Internal.Creators;
using StyletIoC.Internal.Registrations;
using System;
using System.Reflection;

namespace StyletIoC.Internal.Builders
{
    internal abstract class BuilderBindingBase : IInScopeOrWithKeyOrAsWeakBinding, IWithKeyOrAsWeakBinding
    {
        protected Type ServiceType { get; set; }
        protected RegistrationFactory RegistrationFactory { get; set; }
        public string Key { get; protected set; }
        public bool IsWeak { get; protected set; }

        protected BuilderBindingBase(Type serviceType)
        {
            this.ServiceType = serviceType;

            // Default is transient
            this.RegistrationFactory = (ctx, service, creator, key) => new TransientRegistration(creator);
        }

        public IAsWeakBinding WithRegistrationFactory(RegistrationFactory registrationFactory)
        {
            if (registrationFactory == null)
                throw new ArgumentNullException("registrationFactory");
            this.RegistrationFactory = registrationFactory;
            return this;
        }

        /// <summary>
        /// Modify the scope of the binding to Singleton. One instance of this implementation will be generated for this binding.
        /// </summary>
        /// <returns>Fluent interface to continue configuration</returns>
        public IAsWeakBinding InSingletonScope()
        {
            return this.WithRegistrationFactory((ctx, serviceType, creator, key) => new SingletonRegistration(ctx, creator));
        }

        public IInScopeOrAsWeakBinding WithKey(string key)
        {
            this.Key = key;
            return this;
        }

        protected void EnsureType(Type implementationType, Type serviceType = null, bool assertImplementation = true)
        {
            serviceType = serviceType ?? this.ServiceType;

            if (assertImplementation && (!implementationType.IsClass || implementationType.IsAbstract))
                throw new StyletIoCRegistrationException(String.Format("Type {0} is not a concrete class, and so can't be used to implemented service {1}", implementationType.GetDescription(), serviceType.GetDescription()));

            // Test this first, as it's a bit clearer than hitting 'type doesn't implement service'
            if (assertImplementation && implementationType.IsGenericTypeDefinition)
            {
                if (!serviceType.IsGenericTypeDefinition)
                    throw new StyletIoCRegistrationException(String.Format("You can't use an unbound generic type to implement anything that isn't an unbound generic service. Service: {0}, Type: {1}", serviceType.GetDescription(), implementationType.GetDescription()));

                // This restriction may change when I figure out how to pass down the correct type argument
                if (serviceType.GetTypeInfo().GenericTypeParameters.Length != implementationType.GetTypeInfo().GenericTypeParameters.Length)
                    throw new StyletIoCRegistrationException(String.Format("If you're registering an unbound generic type to an unbound generic service, both service and type must have the same number of type parameters. Service: {0}, Type: {1}", serviceType.GetDescription(), implementationType.GetDescription()));
            }
            else if (serviceType.IsGenericTypeDefinition)
            {
                if (implementationType.GetGenericArguments().Length > 0)
                    throw new StyletIoCRegistrationException(String.Format("You cannot bind the bound generic type {0} to the unbound generic service {1}", implementationType.GetDescription(), serviceType.GetDescription()));
                else
                    throw new StyletIoCRegistrationException(String.Format("You cannot bind the non-generic type {0} to the unbound generic service {1}", implementationType.GetDescription(), serviceType.GetDescription()));
            }

            if (!implementationType.Implements(this.ServiceType))
                throw new StyletIoCRegistrationException(String.Format("Type {0} does not implement service {1}", implementationType.GetDescription(), serviceType.GetDescription()));
        }

        // Convenience...
        protected void BindImplementationToService(Container container, Type implementationType, Type serviceType = null)
        {
            serviceType = serviceType ?? this.ServiceType;

            if (serviceType.IsGenericTypeDefinition)
            {
                var unboundGeneric = new UnboundGeneric(serviceType, implementationType, container, this.RegistrationFactory);
                container.AddUnboundGeneric(new TypeKey(serviceType, this.Key), unboundGeneric);
            }
            else
            {
                var creator = new TypeCreator(implementationType, container);
                var registration = this.CreateRegistration(container, creator);

                container.AddRegistration(new TypeKey(serviceType, this.Key ?? creator.AttributeKey), registration);
            }
        }

        // Convenience...
        protected IRegistration CreateRegistration(IRegistrationContext registrationContext, ICreator creator)
        {
            return this.RegistrationFactory(registrationContext, this.ServiceType, creator, this.Key);
        }

        IAsWeakBinding IWithKeyOrAsWeakBinding.WithKey(string key)
        {
            this.Key = key;
            return this;
        }

        void IAsWeakBinding.AsWeakBinding()
        {
            this.IsWeak = true;
        }

        public abstract void Build(Container container);
    }
}

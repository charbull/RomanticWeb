﻿using System.Collections.Generic;

namespace RomanticWeb.Linq.Model.Navigators
{
    /// <summary>Navigates unbound constrain.</summary>
    internal class UnboundConstrainNavigator:EntityConstrainNavigator
    {
        #region Constructors
        /// <summary>Default constructor with nagivated unbound constrain.</summary>
        /// <param name="unboundConstrain">Nagivated unbound constrain.</param>
        internal UnboundConstrainNavigator(UnboundConstrain unboundConstrain):base(unboundConstrain)
        {
        }
        #endregion

        #region Properties
        /// <summary>Gets a navigated component.</summary>
        public UnboundConstrain NavigatedComponent { get { return (UnboundConstrain)((IQueryComponentNavigator)this).NavigatedComponent; } }
        #endregion

        #region Public methods
        /// <summary>Determines if the given component can accept another component as a child.</summary>
        /// <param name="component">Component to be added.</param>
        /// <returns><b>true</b> if given component can be added, otherwise <b>false</b>.</returns>
        public override bool CanAddComponent(IQueryComponent component)
        {
            return (component is IExpression)&&((NavigatedComponent.Subject==null)||(base.CanAddComponent(component)));
        }

        /// <summary>Determines if the given component contains another component as a child.</summary>
        /// <param name="component">Component to be checked.</param>
        /// <returns><b>true</b> if given component is already contained, otherwise <b>false</b>.</returns>
        public override bool ContainsComponent(IQueryComponent component)
        {
            return (NavigatedComponent.Subject==component)||(base.ContainsComponent(component));
        }

        /// <summary>Adds component as a child of another component.</summary>
        /// <param name="component">Component to be added.</param>
        public override void AddComponent(IQueryComponent component)
        {
            if (component is IExpression)
            {
                if (NavigatedComponent.Subject==null)
                {
                    NavigatedComponent.Subject=(IExpression)component;
                }
                else
                {
                    base.AddComponent(component);
                }
            }
        }

        /// <summary>Retrieves all child components.</summary>
        /// <returns>Enumeration of all child components.</returns>
        public override IEnumerable<IQueryComponent> GetComponents()
        {
            List<IQueryComponent> result=new List<IQueryComponent>();
            if (NavigatedComponent.Subject!=null)
            {
                result.Add(NavigatedComponent.Subject);
            }

            result.AddRange(base.GetComponents());
            return result.AsReadOnly();
        }
        #endregion
    }
}
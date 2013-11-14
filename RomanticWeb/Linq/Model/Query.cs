﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using NullGuard;
using RomanticWeb.Linq.Model.Navigators;

namespace RomanticWeb.Linq.Model
{
    /// <summary>Represents a whole query.</summary>
    [QueryComponentNavigator(typeof(QueryNavigator))]
    public class Query:QueryElement,IExpression
    {
        #region Fieds
        private IList<Prefix> _prefixes;
        private IList<ISelectableQueryComponent> _select;
        private IList<QueryElement> _elements;
        private IVariableNamingStrategy _variableNamingStrategy;
        private Identifier _subject;
        #endregion

        #region Constructors
        /// <summary>Default parameterles constructor</summary>
        internal Query():this(null)
        {
        }

        /// <summary>Constrctor with subject passed.</summary>
        /// <param name="subject">Subject of this query.</param>
        internal Query(Identifier subject):base()
        {
            ObservableCollection<Prefix> prefixes=new ObservableCollection<Prefix>();
            prefixes.CollectionChanged+=OnCollectionChanged;
            _prefixes=prefixes;
            ObservableCollection<ISelectableQueryComponent> select=new ObservableCollection<ISelectableQueryComponent>();
            select.CollectionChanged+=OnCollectionChanged;
            _select=select;
            ObservableCollection<QueryElement> elements=new ObservableCollection<QueryElement>();
            elements.CollectionChanged+=OnCollectionChanged;
            _elements=elements;
            _variableNamingStrategy=new UniqueVariableNamingStrategy(this);
            if ((_subject=subject)!=null)
            {
                _subject.OwnerQuery=this;
            }
        }

        /// <summary>Constructor with subject and variable naming strategy passed.</summary>
        /// <param name="subject">Subject of this query.</param>
        /// <param name="variableNamingStrategy">Varialbe naming strategy.</param>
        internal Query(Identifier subject,IVariableNamingStrategy variableNamingStrategy):this(subject)
        {
            _variableNamingStrategy=variableNamingStrategy;
        }
        #endregion

        #region Properties
        /// <summary>Gets an enumeration of all prefixes.</summary>
        public IList<Prefix> Prefixes { get { return _prefixes; } }

        /// <summary>Gets an enumeration of all selected expressions.</summary>
        public IList<ISelectableQueryComponent> Select { get { return _select; } }

        /// <summary>Gets an enumeration of all query elements.</summary>
        public IList<QueryElement> Elements { get { return _elements; } }

        /// <summary>Gets a value indicating if the given query is actually a sub query.</summary>
        public bool IsSubQuery { get { return (OwnerQuery!=null); } }

        /// <summary>Gets an owning query.</summary>
        internal override Query OwnerQuery
        {
            get
            {
                return base.OwnerQuery;
            }

            set
            {
                if (value!=null)
                {
                    base.OwnerQuery=value;
                }
            }
        }

        /// <summary>Subject of this query.</summary>
        [AllowNull]
        internal Identifier Subject
        {
            get
            {
                return _subject;
            }

            set
            {
                if (value==null)
                {
                    throw new ArgumentNullException("subject");
                }

                (_subject=value).OwnerQuery=this;
            }
        }
        #endregion

        #region Public methods
        /// <summary>Creates a new blank query that can act as a sub query for this instance.</summary>
        /// <param name="subject">Primary subject of the resulting query.</param>
        /// <remarks>This method doesn't add the resulting query as a sub query of this instance.</remarks>
        /// <returns>Query that can act as a sub query for this instance.</returns>
        public Query CreateSubQuery(Identifier subject)
        {
            Query result=new Query(subject,_variableNamingStrategy);
            result.OwnerQuery=this;
            return result;
        }

        /// <summary>Creates a variable name from given identifier.</summary>
        /// <param name="identifier">Identifier to be used to abbreviate variable name.</param>
        /// <returns>Variable name with unique name.</returns>
        public string CreateVariableName(string identifier)
        {
            return _variableNamingStrategy.GetNameForIdentifier(identifier);
        }

        /// <summary>Retrieves an identifier from a passed variable name.</summary>
        /// <param name="variableName">Variable name to retrieve identifier from.</param>
        /// <returns>Identifier passed to create the variable name.</returns>
        public string RetrieveIdentifier(string variableName)
        {
            return _variableNamingStrategy.ResolveNameToIdentifier(variableName);
        }

        /// <summary>Creates a string representation of this query.</summary>
        /// <returns>String representation of this query.</returns>
        public override string ToString()
        {
            return System.String.Format(
                "{3} SELECT {1} {0}WHERE {0}{{{0}{2}{0}}}",
                Environment.NewLine,
                System.String.Join(" ",_select.Select(item => (item is EntityAccessor?System.String.Format("?G{0} ?{0}",((EntityAccessor)item).About.Name):item.ToString()))),
                System.String.Join(Environment.NewLine,_elements),
                System.String.Join(Environment.NewLine,_prefixes));
        }
        #endregion

        #region Non-public methods
        /// <summary>Rised when arguments collection has changed.</summary>
        /// <param name="sender">Sender of this event.</param>
        /// <param name="e">Eventarguments.</param>
        protected virtual void OnCollectionChanged(object sender,NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    {
                        foreach (QueryComponent queryComponent in e.NewItems)
                        {
                            if (queryComponent!=null)
                            {
                                queryComponent.OwnerQuery=this;
                            }
                        }

                        break;
                    }
            }
        }
        #endregion
    }
}
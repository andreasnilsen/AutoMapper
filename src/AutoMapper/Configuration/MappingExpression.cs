using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AutoMapper.Internal;

namespace AutoMapper.Configuration
{
    public class MappingExpression : MappingExpressionBase<object, object, IMappingExpression>, IMappingExpression
    {
        public MappingExpression(TypePair types, MemberList memberList) : base(memberList, types)
        {
        }

        public void ConvertUsing<TTypeConverter>() 
            => ConvertUsing(typeof(TTypeConverter));

        public IMappingExpression ReverseMap()
        {
            var reverseMap = new MappingExpression(new TypePair(Types.DestinationType, Types.SourceType), MemberList.Source);
            reverseMap.MemberConfigurations.AddRange(MemberConfigurations.Select(m => m.Reverse()).Where(m => m != null));
            ReverseMapExpression = reverseMap;
            return reverseMap;
        }

        public void ForAllMembers(Action<IMemberConfigurationExpression> memberOptions)
        {
            TypeMapActions.Add(typeMap =>
            {
                foreach (var accessor in typeMap.DestinationTypeDetails.PublicReadAccessors)
                {
                    ForMember(accessor, memberOptions);
                }
            });
        }

        public void ForAllOtherMembers(Action<IMemberConfigurationExpression> memberOptions)
        {
            TypeMapActions.Add(typeMap =>
            {
                foreach (var accessor in typeMap.DestinationTypeDetails.PublicReadAccessors.Where(m =>
                    GetDestinationMemberConfiguration(m) == null))
                {
                    ForMember(accessor, memberOptions);
                }
            });
        }

        public IMappingExpression ForMember(string name, Action<IMemberConfigurationExpression> memberOptions)
        {
            var member = DestinationType.GetFieldOrProperty(name);
            ForMember(member, memberOptions);
            return this;
        }

        protected override void IgnoreDestinationMember(MemberInfo property, bool ignorePaths = true)
        {
            ForMember(property, _ => {}).Ignore(ignorePaths);
        }

        internal MemberConfigurationExpression ForMember(MemberInfo destinationProperty, Action<IMemberConfigurationExpression> memberOptions)
        {
            var expression = new MemberConfigurationExpression(destinationProperty, Types.SourceType);

            MemberConfigurations.Add(expression);

            memberOptions(expression);

            return expression;
        }

        internal class MemberConfigurationExpression : MemberConfigurationExpression<object, object, object>, IMemberConfigurationExpression
        {
            public MemberConfigurationExpression(MemberInfo destinationMember, Type sourceType)
                : base(destinationMember, sourceType)
            {
            }

            public void MapFrom(Type valueResolverType)
            {
                var config = new ValueResolverConfiguration(valueResolverType, valueResolverType.GetGenericInterface(typeof(IValueResolver<,,>)));

                PropertyMapActions.Add(pm => pm.ValueResolverConfig = config);
            }

            public void MapFrom(Type valueResolverType, string sourceMemberName)
            {
                var config = new ValueResolverConfiguration(valueResolverType, valueResolverType.GetGenericInterface(typeof(IMemberValueResolver<,,,>)))
                {
                    SourceMemberName = sourceMemberName
                };

                PropertyMapActions.Add(pm => pm.ValueResolverConfig = config);
            }

            public void MapFrom<TSource, TDestination, TSourceMember, TDestMember>(IMemberValueResolver<TSource, TDestination, TSourceMember, TDestMember> resolver, string sourceMemberName)
            {
                var config = new ValueResolverConfiguration(resolver, typeof(IMemberValueResolver<TSource, TDestination, TSourceMember, TDestMember>))
                {
                    SourceMemberName = sourceMemberName
                };

                PropertyMapActions.Add(pm => pm.ValueResolverConfig = config);
            }

            public void ConvertUsing(Type valueConverterType) 
                => PropertyMapActions.Add(pm => ConvertUsing(pm, valueConverterType));

            public void ConvertUsing(Type valueConverterType, string sourceMemberName) 
                => PropertyMapActions.Add(pm => ConvertUsing(pm, valueConverterType, sourceMemberName));

            public void ConvertUsing<TSourceMember, TDestinationMember>(IValueConverter<TSourceMember, TDestinationMember> valueConverter, string sourceMemberName)
            {
                PropertyMapActions.Add(pm =>
                {
                    var config = new ValueConverterConfiguration(valueConverter, typeof(IValueConverter<TSourceMember, TDestinationMember>))
                    {
                        SourceMemberName = sourceMemberName
                    };

                    pm.ValueConverterConfig = config;
                });
            }

            private static void ConvertUsing(PropertyMap propertyMap, Type valueConverterType, string sourceMemberName = null)
            {
                var config = new ValueConverterConfiguration(valueConverterType, valueConverterType.GetGenericInterface(typeof(IValueConverter<,>)))
                {
                    SourceMemberName = sourceMemberName
                };

                propertyMap.ValueConverterConfig = config;
            }
        }
    }

    public class MappingExpression<TSource, TDestination> :
        MappingExpressionBase<TSource, TDestination, IMappingExpression<TSource, TDestination>>,
        IMappingExpression<TSource, TDestination>
    {

        public MappingExpression(MemberList memberList)
            : base(memberList)
        {
        }

        public MappingExpression(MemberList memberList, Type sourceType, Type destinationType)
            : base(memberList, sourceType, destinationType)
        {
        }

        public MappingExpression(MemberList memberList, TypePair types)
            : base(memberList, types)
        {
        }

        public IMappingExpression<TSource, TDestination> ForPath<TMember>(Expression<Func<TDestination, TMember>> destinationMember,
            Action<IPathConfigurationExpression<TSource, TDestination, TMember>> memberOptions)
        {
            if(!destinationMember.IsMemberPath())
            {
                throw new ArgumentOutOfRangeException(nameof(destinationMember), "Only member accesses are allowed.");
            }
            var expression = new PathConfigurationExpression<TSource, TDestination, TMember>(destinationMember);
            var firstMember = expression.MemberPath.First;
            var firstMemberConfig = GetDestinationMemberConfiguration(firstMember);
            if(firstMemberConfig == null)
            {
                IgnoreDestinationMember(firstMember, ignorePaths: false);
            }
            MemberConfigurations.Add(expression);
            memberOptions(expression);
            return this;
        }

        public IMappingExpression<TSource, TDestination> ForMember<TMember>(Expression<Func<TDestination, TMember>> destinationMember, Action<IMemberConfigurationExpression<TSource, TDestination, TMember>> memberOptions)
        {
            var memberInfo = ReflectionHelper.FindProperty(destinationMember);
            return ForDestinationMember(memberInfo, memberOptions);
        }

        public IMappingExpression<TSource, TDestination> ForMember(string name, Action<IMemberConfigurationExpression<TSource, TDestination, object>> memberOptions)
        {
            var member = DestinationType.GetFieldOrProperty(name);
            return ForDestinationMember(member, memberOptions);
        }

        public void ForAllOtherMembers(Action<IMemberConfigurationExpression<TSource, TDestination, object>> memberOptions)
        {
            TypeMapActions.Add(typeMap =>
            {
                foreach (var accessor in typeMap.DestinationTypeDetails.PublicReadAccessors.Where(m =>
                    GetDestinationMemberConfiguration(m) == null))
                {
                    ForDestinationMember(accessor, memberOptions);
                }
            });
        }

        public void ForAllMembers(Action<IMemberConfigurationExpression<TSource, TDestination, object>> memberOptions)
        {
            TypeMapActions.Add(typeMap =>
            {
                foreach (var accessor in typeMap.DestinationTypeDetails.PublicReadAccessors)
                {
                    ForDestinationMember(accessor, memberOptions);
                }
            });
        }

        public IMappingExpression<TSource, TDestination> Include<TOtherSource, TOtherDestination>()
            where TOtherSource : TSource
            where TOtherDestination : TDestination
        {
            IncludeCore(typeof(TOtherSource), typeof(TOtherDestination));
            
            return this;
        }

        public IMappingExpression<TSource, TDestination> IncludeBase<TSourceBase, TDestinationBase>() 
            => IncludeBase(typeof(TSourceBase), typeof(TDestinationBase));

        public IMappingExpression<TSource, TDestination> ForSourceMember(Expression<Func<TSource, object>> sourceMember, Action<ISourceMemberConfigurationExpression> memberOptions)
        {
            var memberInfo = ReflectionHelper.FindProperty(sourceMember);

            var srcConfig = new SourceMappingExpression(memberInfo);

            memberOptions(srcConfig);

            SourceMemberConfigurations.Add(srcConfig);

            return this;
        }

        public void ConvertUsing(Func<TSource, TDestination, TDestination> mappingFunction)
        {
            TypeMapActions.Add(tm =>
            {
                Expression<Func<TSource, TDestination, ResolutionContext, TDestination>> expr =
                    (src, dest, ctxt) => mappingFunction(src, dest);

                tm.CustomMapFunction = expr;
            });
        }

        public void ConvertUsing(Func<TSource, TDestination, ResolutionContext, TDestination> mappingFunction)
        {
            TypeMapActions.Add(tm =>
            {
                Expression<Func<TSource, TDestination, ResolutionContext, TDestination>> expr =
                    (src, dest, ctxt) => mappingFunction(src, dest, ctxt);

                tm.CustomMapFunction = expr;
            });
        }

        public void ConvertUsing(ITypeConverter<TSource, TDestination> converter)
        {
            ConvertUsing(converter.Convert);
        }

        public void ConvertUsing<TTypeConverter>() where TTypeConverter : ITypeConverter<TSource, TDestination>
        {
            TypeMapActions.Add(tm => tm.TypeConverterType = typeof (TTypeConverter));
        }

        public void As<T>() where T : TDestination => As(typeof(T));

        public IMappingExpression<TSource, TDestination> AddTransform<TValue>(Expression<Func<TValue, TValue>> transformer)
        {
            var config = new ValueTransformerConfiguration(typeof(TValue), transformer);

            ValueTransformers.Add(config);

            return this;
        }

        public IMappingExpression<TDestination, TSource> ReverseMap()
        {
            var reverseMap = new MappingExpression<TDestination, TSource>(MemberList.None, Types.DestinationType, Types.SourceType);
            reverseMap.MemberConfigurations.AddRange(MemberConfigurations.Select(m => m.Reverse()).Where(m => m != null));
            ReverseMapExpression = reverseMap;
            return reverseMap;
        }

        private IMappingExpression<TSource, TDestination> ForDestinationMember<TMember>(MemberInfo destinationProperty, Action<MemberConfigurationExpression<TSource, TDestination, TMember>> memberOptions)
        {
            var expression = new MemberConfigurationExpression<TSource, TDestination, TMember>(destinationProperty, Types.SourceType);

            MemberConfigurations.Add(expression);

            memberOptions(expression);

            return this;
        }

        protected override void IgnoreDestinationMember(MemberInfo property, bool ignorePaths = true) 
            => ForDestinationMember<object>(property, options => options.Ignore(ignorePaths));
    }
}


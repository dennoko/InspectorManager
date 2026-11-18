using System;
using System.Collections.Generic;

namespace InspectorManager.Core
{
    /// <summary>
    /// シンプルな依存性注入コンテナ。
    /// サービスの登録と解決を行う。
    /// </summary>
    public class ServiceLocator
    {
        private static ServiceLocator _instance;
        public static ServiceLocator Instance => _instance ??= new ServiceLocator();

        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        private readonly Dictionary<Type, Func<object>> _factories = new Dictionary<Type, Func<object>>();
        private readonly object _lock = new object();

        /// <summary>
        /// シングルトンサービスを登録する
        /// </summary>
        public void Register<TInterface, TImplementation>(TImplementation instance)
            where TImplementation : TInterface
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            lock (_lock)
            {
                _services[typeof(TInterface)] = instance;
            }
        }

        /// <summary>
        /// シングルトンサービスを登録する（インターフェースなし）
        /// </summary>
        public void Register<T>(T instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            lock (_lock)
            {
                _services[typeof(T)] = instance;
            }
        }

        /// <summary>
        /// ファクトリーを登録する（遅延初期化用）
        /// </summary>
        public void RegisterFactory<TInterface>(Func<TInterface> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            lock (_lock)
            {
                _factories[typeof(TInterface)] = () => factory();
            }
        }

        /// <summary>
        /// サービスを解決する
        /// </summary>
        public T Resolve<T>()
        {
            lock (_lock)
            {
                var type = typeof(T);

                // 既存のインスタンスを確認
                if (_services.TryGetValue(type, out var service))
                {
                    return (T)service;
                }

                // ファクトリーがあれば生成してキャッシュ
                if (_factories.TryGetValue(type, out var factory))
                {
                    var instance = (T)factory();
                    _services[type] = instance;
                    _factories.Remove(type);
                    return instance;
                }

                throw new InvalidOperationException(
                    $"Service of type {type.Name} is not registered.");
            }
        }

        /// <summary>
        /// サービスを解決する（存在しない場合はnull）
        /// </summary>
        public T TryResolve<T>() where T : class
        {
            lock (_lock)
            {
                var type = typeof(T);

                if (_services.TryGetValue(type, out var service))
                {
                    return (T)service;
                }

                if (_factories.TryGetValue(type, out var factory))
                {
                    var instance = (T)factory();
                    _services[type] = instance;
                    _factories.Remove(type);
                    return instance;
                }

                return null;
            }
        }

        /// <summary>
        /// サービスが登録されているか確認
        /// </summary>
        public bool IsRegistered<T>()
        {
            lock (_lock)
            {
                var type = typeof(T);
                return _services.ContainsKey(type) || _factories.ContainsKey(type);
            }
        }

        /// <summary>
        /// すべてのサービスをクリア
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                // IDisposableを実装しているサービスはDisposeする
                foreach (var service in _services.Values)
                {
                    if (service is IDisposable disposable)
                    {
                        try
                        {
                            disposable.Dispose();
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogException(ex);
                        }
                    }
                }

                _services.Clear();
                _factories.Clear();
            }
        }

        /// <summary>
        /// 登録を解除する
        /// </summary>
        public void Unregister<T>()
        {
            lock (_lock)
            {
                var type = typeof(T);
                if (_services.TryGetValue(type, out var service))
                {
                    if (service is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    _services.Remove(type);
                }
                _factories.Remove(type);
            }
        }
    }
}

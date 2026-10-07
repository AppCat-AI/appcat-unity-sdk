package ai.appcat.unity

import android.app.Activity
import com.appcat.core.AppCatCore

/**
 * AppCatUnityBridge — Android native bridge for the Unity SDK.
 *
 * Static entry points called from C# via `AndroidJavaClass.CallStatic`.
 * Delegates every call to `com.appcat.core.AppCatCore` (vendored AAR) and
 * returns results as JSON strings, asynchronously through [Callback], which
 * the C# side implements with an `AndroidJavaProxy`.
 *
 * Hardening contract (same as the React Native module):
 *   - Every entry point runs inside try/catch; unexpected throwables become an
 *     `ERR_INTERNAL` callback instead of crashing the Unity activity.
 *   - Fire-and-forget methods swallow exceptions.
 *   - Each callback fires exactly once.
 */
object AppCatUnityBridge {

  /** Implemented in C# (`AndroidJavaProxy`). Invoked on an arbitrary thread. */
  interface Callback {
    fun onSuccess(json: String?)
    fun onError(code: String, message: String)
  }

  private const val ERR_INTERNAL = "ERR_INTERNAL"
  private const val ERR_SET_TRACKING_CONSENT = "ERR_SET_TRACKING_CONSENT"

  private fun Callback.safeSuccess(json: String?) {
    try { onSuccess(json) } catch (_: Throwable) { /* host consumed */ }
  }

  private fun Callback.safeError(code: String, message: String) {
    try { onError(code, message) } catch (_: Throwable) { /* host consumed */ }
  }

  // MARK: - configure

  @JvmStatic
  fun configure(activity: Activity, appId: String, apiKey: String, optionsJson: String?, callback: Callback) {
    try {
      val options = AppCatUnityJson.parseObject(optionsJson) ?: emptyMap()
      AppCatCore.instance.configure(
        context = activity.applicationContext,
        appId = appId,
        apiKey = apiKey,
        options = options,
        callback = object : AppCatCore.ResultCallback<Boolean> {
          override fun onSuccess(result: Boolean) {
            try {
              AppCatCore.instance.resolve(
                ddlToken = null,
                callback = object : AppCatCore.ResultCallback<Map<String, Any?>> {
                  override fun onSuccess(result: Map<String, Any?>) {
                    callback.safeSuccess(AppCatUnityJson.stringify(AppCatUnityJson.initResponse(result)) ?: "{}")
                  }
                  override fun onError(code: String, message: String) {
                    // Resolve failure is non-fatal; configure still succeeded.
                    callback.safeSuccess(AppCatUnityJson.stringify(AppCatUnityJson.initResponse(null)) ?: "{}")
                  }
                }
              )
            } catch (_: Throwable) {
              callback.safeSuccess(AppCatUnityJson.stringify(AppCatUnityJson.initResponse(null)) ?: "{}")
            }
          }
          override fun onError(code: String, message: String) {
            callback.safeError(code, message)
          }
        }
      )
    } catch (t: Throwable) {
      callback.safeError(ERR_INTERNAL, t.message ?: t.javaClass.simpleName)
    }
  }

  // MARK: - identify

  @JvmStatic
  fun identify(dataJson: String?, callback: Callback) {
    try {
      val data = AppCatUnityJson.parseObject(dataJson) ?: emptyMap()
      AppCatCore.instance.identify(
        data = data,
        callback = object : AppCatCore.ResultCallback<Map<String, Any?>?> {
          override fun onSuccess(result: Map<String, Any?>?) {
            callback.safeSuccess(AppCatUnityJson.stringify(result))
          }
          override fun onError(code: String, message: String) {
            callback.safeError(code, message)
          }
        }
      )
    } catch (t: Throwable) {
      callback.safeError(ERR_INTERNAL, t.message ?: t.javaClass.simpleName)
    }
  }

  // MARK: - sendEvent (fire-and-forget)

  @JvmStatic
  fun sendEvent(eventName: String?, paramsJson: String?, optionsJson: String?) {
    try {
      if (eventName.isNullOrEmpty()) return
      AppCatCore.instance.sendEvent(
        eventName = eventName,
        params = AppCatUnityJson.parseObject(paramsJson),
        options = AppCatUnityJson.parseObject(optionsJson),
      )
    } catch (_: Throwable) {
      // never crash the host
    }
  }

  // MARK: - synchronous getters

  @JvmStatic
  fun getAttribution(): String? = try {
    AppCatUnityJson.stringify(AppCatCore.instance.getAttribution())
  } catch (_: Throwable) { null }

  @JvmStatic
  fun getDeviceContext(): String? = try {
    AppCatUnityJson.stringify(AppCatCore.instance.getDeviceContext())
  } catch (_: Throwable) { null }

  @JvmStatic
  fun getAppCatId(): String = try {
    AppCatCore.instance.getAppCatId()
  } catch (_: Throwable) { "" }

  @JvmStatic
  fun isDisabled(): Boolean = try {
    AppCatCore.instance.isDisabled()
  } catch (_: Throwable) { false }

  @JvmStatic
  fun setLogLevel(level: Int) {
    try { AppCatCore.instance.setLogLevel(level) } catch (_: Throwable) { /* best effort */ }
  }

  // MARK: - setTrackingConsent

  @JvmStatic
  fun setTrackingConsent(granted: Boolean, callback: Callback) {
    try {
      AppCatCore.instance.setTrackingConsent(granted) { result ->
        if (result.isSuccess) {
          callback.safeSuccess(null)
        } else {
          val err = result.exceptionOrNull()
          callback.safeError(ERR_SET_TRACKING_CONSENT, err?.message ?: "setTrackingConsent failed")
        }
      }
    } catch (t: Throwable) {
      callback.safeError(ERR_SET_TRACKING_CONSENT, t.message ?: "setTrackingConsent failed")
    }
  }
}

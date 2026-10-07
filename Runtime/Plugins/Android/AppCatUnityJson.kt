package ai.appcat.unity

import org.json.JSONArray
import org.json.JSONObject

/**
 * JSON helpers for the Unity ↔ Kotlin boundary. Depends only on `org.json`
 * (bundled with Android) so it can be unit-tested on the JVM without the
 * AppCat core AAR.
 *
 * Hardening: never throws. Malformed input degrades to `null` / empty.
 */
object AppCatUnityJson {

  /** Parses a JSON object string into a mutable map. Non-object JSON yields `null`. */
  @JvmStatic
  fun parseObject(json: String?): Map<String, Any?>? {
    if (json.isNullOrBlank()) return null
    return try {
      toMap(JSONObject(json))
    } catch (_: Throwable) {
      null
    }
  }

  /** Serializes a map to compact JSON. Returns `null` when the map is null or not encodable. */
  @JvmStatic
  fun stringify(map: Map<String, Any?>?): String? {
    if (map == null) return null
    return try {
      toJsonObject(map).toString()
    } catch (_: Throwable) {
      null
    }
  }

  /**
   * Shapes a core `resolve()` result into the `{ deepLinkParams, geo }` envelope
   * shared by every SDK's `init()` response. Empty params collapse to `null`.
   */
  @JvmStatic
  fun initResponse(resolveResult: Map<String, Any?>?): Map<String, Any?> {
    val params = resolveResult?.get("deepLinkParams") as? Map<*, *>
    val geo = resolveResult?.get("geo")
    return mapOf(
      "deepLinkParams" to (if (params != null && params.isNotEmpty()) params else null),
      "geo" to geo,
    )
  }

  // ---------------------------------------------------------------------
  // Conversion
  // ---------------------------------------------------------------------

  private fun toMap(obj: JSONObject): Map<String, Any?> {
    val out = LinkedHashMap<String, Any?>()
    val keys = obj.keys()
    while (keys.hasNext()) {
      val key = keys.next()
      out[key] = fromJson(obj.opt(key))
    }
    return out
  }

  private fun toList(arr: JSONArray): List<Any?> {
    val out = ArrayList<Any?>(arr.length())
    for (i in 0 until arr.length()) out.add(fromJson(arr.opt(i)))
    return out
  }

  private fun fromJson(value: Any?): Any? = when (value) {
    null, JSONObject.NULL -> null
    is JSONObject -> toMap(value)
    is JSONArray -> toList(value)
    else -> value
  }

  private fun toJsonObject(map: Map<*, *>): JSONObject {
    val obj = JSONObject()
    for ((k, v) in map) {
      if (k !is String) continue
      obj.put(k, toJsonValue(v))
    }
    return obj
  }

  private fun toJsonValue(value: Any?): Any = when (value) {
    null -> JSONObject.NULL
    is Map<*, *> -> toJsonObject(value)
    is Iterable<*> -> JSONArray().also { arr -> value.forEach { arr.put(toJsonValue(it)) } }
    is Array<*> -> JSONArray().also { arr -> value.forEach { arr.put(toJsonValue(it)) } }
    is Boolean, is Number, is String -> value
    else -> value.toString()
  }
}

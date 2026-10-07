package ai.appcat.unity

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class AppCatUnityJsonTest {

  // parseObject

  @Test
  fun parseObjectReturnsMap() {
    val map = AppCatUnityJson.parseObject("""{"isDebug":true,"logLevel":0,"customerUserId":"u1","nested":{"a":[1,2]}}""")
    assertNotNull(map)
    assertEquals(true, map!!["isDebug"])
    assertEquals(0, map["logLevel"])
    assertEquals("u1", map["customerUserId"])
    @Suppress("UNCHECKED_CAST")
    val nested = map["nested"] as Map<String, Any?>
    assertEquals(listOf(1, 2), nested["a"])
  }

  @Test
  fun parseObjectMapsJsonNullToKotlinNull() {
    val map = AppCatUnityJson.parseObject("""{"x":null}""")
    assertTrue(map!!.containsKey("x"))
    assertNull(map["x"])
  }

  @Test
  fun parseObjectRejectsGarbageAndNonObjects() {
    assertNull(AppCatUnityJson.parseObject(null))
    assertNull(AppCatUnityJson.parseObject(""))
    assertNull(AppCatUnityJson.parseObject("{"))
    assertNull(AppCatUnityJson.parseObject("[1,2]"))
  }

  // stringify

  @Test
  fun stringifyRoundTrips() {
    val json = AppCatUnityJson.stringify(
      mapOf("a" to 1, "b" to "x", "c" to listOf(1, 2), "d" to mapOf("k" to null), "e" to arrayOf("z"))
    )
    assertNotNull(json)
    val back = AppCatUnityJson.parseObject(json)!!
    assertEquals(1, back["a"])
    assertEquals("x", back["b"])
    assertEquals(listOf(1, 2), back["c"])
    @Suppress("UNCHECKED_CAST")
    assertNull((back["d"] as Map<String, Any?>)["k"])
    assertEquals(listOf("z"), back["e"])
  }

  @Test
  fun stringifyNullReturnsNull() {
    assertNull(AppCatUnityJson.stringify(null))
  }

  @Test
  fun stringifyStringifiesUnknownLeaves() {
    val json = AppCatUnityJson.stringify(mapOf("when" to java.util.Date(0)))
    assertNotNull(json)
    assertTrue(AppCatUnityJson.parseObject(json)!!["when"] is String)
  }

  // initResponse

  @Test
  fun initResponseShapesMatch() {
    val env = AppCatUnityJson.initResponse(
      mapOf(
        "matched" to true,
        "deepLinkParams" to mapOf("promo" to "summer"),
        "geo" to mapOf("city" to "NYC", "country" to "US", "state" to "NY"),
      )
    )
    @Suppress("UNCHECKED_CAST")
    assertEquals("summer", (env["deepLinkParams"] as Map<String, Any?>)["promo"])
    @Suppress("UNCHECKED_CAST")
    assertEquals("NYC", (env["geo"] as Map<String, Any?>)["city"])
  }

  @Test
  fun initResponseCollapsesEmptyParamsToNull() {
    val env = AppCatUnityJson.initResponse(mapOf("deepLinkParams" to emptyMap<String, Any?>()))
    assertNull(env["deepLinkParams"])
    assertNull(env["geo"])
  }

  @Test
  fun initResponseNullInputIsSerializable() {
    val env = AppCatUnityJson.initResponse(null)
    assertEquals(setOf("deepLinkParams", "geo"), env.keys)
    val json = AppCatUnityJson.stringify(env)
    assertNotNull(json)
    // org.json does not preserve key order; compare parsed content.
    val back = AppCatUnityJson.parseObject(json)!!
    assertEquals(setOf("deepLinkParams", "geo"), back.keys)
    assertNull(back["deepLinkParams"])
    assertNull(back["geo"])
  }
}

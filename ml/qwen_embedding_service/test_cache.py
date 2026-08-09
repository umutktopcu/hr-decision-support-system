import unittest
from main import _embedding_cache, _generate_cache_key, LRUCache

class TestEmbeddingCache(unittest.TestCase):
    def setUp(self):
        # Reset cache before each test
        _embedding_cache.clear()

    def test_cache_hit_and_miss(self):
        key = _generate_cache_key("test text", "document")
        self.assertIsNone(_embedding_cache.get(key))
        self.assertEqual(_embedding_cache.misses, 1)

        _embedding_cache.put(key, [0.1, 0.2, 0.3])
        val = _embedding_cache.get(key)
        self.assertEqual(val, [0.1, 0.2, 0.3])
        self.assertEqual(_embedding_cache.hits, 1)
        self.assertEqual(_embedding_cache.entries, 1)

    def test_query_document_mode_separation(self):
        text = "identical text"
        key_doc = _generate_cache_key(text, "document")
        key_query = _generate_cache_key(text, "query")

        self.assertNotEqual(key_doc, key_query)

        _embedding_cache.put(key_doc, [1.0])
        self.assertIsNone(_embedding_cache.get(key_query))

    def test_cache_limit(self):
        small_cache = LRUCache(2)

        key1 = _generate_cache_key("1", "document")
        key2 = _generate_cache_key("2", "document")
        key3 = _generate_cache_key("3", "document")

        small_cache.put(key1, [1.0])
        small_cache.put(key2, [2.0])
        self.assertEqual(small_cache.entries, 2)

        # Access key1 to make it most recently used
        small_cache.get(key1)

        # Add key3, which should evict key2 (least recently used)
        small_cache.put(key3, [3.0])

        self.assertEqual(small_cache.entries, 2)
        self.assertIsNotNone(small_cache.get(key1))
        self.assertIsNotNone(small_cache.get(key3))
        self.assertIsNone(small_cache.get(key2))

if __name__ == '__main__':
    unittest.main()

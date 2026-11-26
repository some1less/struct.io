import { describe, expect, it } from 'vitest'
import { productUrl } from './format'

describe('productUrl', () => {
  it('keeps an https product link', () => {
    expect(productUrl('https://www.x-kom.pl/p/1.html')).toBe('https://www.x-kom.pl/p/1.html')
  })

  it.each([null, undefined, '', 'http://x-kom.pl/p/1.html', 'javascript:alert(1)'])(
    'drops %s',
    (url) => {
      expect(productUrl(url)).toBeNull()
    },
  )
})

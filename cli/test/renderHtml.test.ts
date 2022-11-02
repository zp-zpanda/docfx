import { renderHtml } from '../src/renderHtml'

describe('renderHtml', () => {
    it('looks good', () => {
        expect(renderHtml({
  id: '1',
  languageId: 'typescript',
  title: '',
  body: [],
  summary: 'This is a summary<script>'
})).toMatchInlineSnapshot(`"<p>This is a summary&lt;script&gt;</p>"`)
    })
})
import escapeHtml from 'escape-html'
import { ReferencePage } from "./reference"

export function renderHtml(page: ReferencePage): string {
    return html`<p>${page.summary}</p>`
}

function html(strings: TemplateStringsArray, ...values: string[]): string {
    let result = ''
    for (let i = 0; i < strings.length; i++) {
        result += strings[i]
        if (i < values.length) {
            result += escapeHtml(values[i])
        }
    }
    return result
}
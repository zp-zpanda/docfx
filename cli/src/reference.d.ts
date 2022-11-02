/**
 * A unique global identifier using variant length string.
 * MUST match this regular expression /[a-zA-Z0-9-_.@/]+/g
 */
export type Id = string

/**
 * A language identifier string following the vscode language identifiers guideline
 */
export type LanguageId = string

/**
 * Displays a string literal with no formatting
 */
export type Plaintext = string

/**
 * Displays text as markdown preferably using CommonMark */
export type Markdown = string

/**
 * A hyperlink to another page
 */
export type Link = {
    /** Display name of the link */
    link: string,

    /** Target URL of the link */
    href: string,
}

/**
 * Displays a single text run that mixes text and links
 */
export type Inline = Plaintext | Link | Inline[]

/**
 * Represents a reference page
 */
export type ReferencePage = {
    /** ID of the page */
    id: Id,

    /** Language identifier of the page */
    languageId: LanguageId,

    /** Title of the page */
    title: Plaintext,

    /** A list of components to display on the page. Components are displayed from top to bottom in the order they appear on the list. */
    body: Component[],

    /** Short summary of the page */
    summary?: Plaintext,

    /** A key-value pair of custom metadata of the page */
    metadata?: object,

    /** Whether this page is deprecated, or the deprecation reason. */
    deprecated?: boolean | Markdown,
}

export type Component = Section | TextBlock | Declaration | Fact | JumpList | ParameterList

/** Displays part of the content with a header. Supports nested sections with a maximum of 2 layers deep. */
export type Section = {
    /** Section header text */
    section: Plaintext,

    /** An ordered list of components in this section */
    body: Component[]
}

/** Displays markdown text block preferably using CommonMark */
export type TextBlock = {
    /** Content in markdown format */
    markdown: Markdown
}

/** Displays a non-actionable block of code declaration */
export type Declaration = {
    /** Content in markdown format */
    declaration: Plaintext,

    /** Syntax highlight language id, use page language id when not specified */
    languageId: LanguageId
}

/** Displays a series of key value pair facts in a tabular form */
export type Fact = {
    /** Key value pair of facts. */
    fact: { [key: Plaintext]: Inline }
}

/** Displays a quick summary of items to jump to in a tabular form */
export type JumpList = {
    /** An ordered list of jump list items. */
    jumplist: {
        /** Item name to jump to */
        name: Inline,

        /** Item description */
        description: Markdown,

        /** Whether this item is deprecated */
        deprecated: boolean,
    }[]
}

/** Displays a list of parameters in an indented list form */
export type ParameterList = {
    /** An ordered list of parameters. */
    parameters: {
        /** The parameter name formatted as code */
        name: Plaintext,

        /** Description of the parameter in markdown */
        description: Markdown,

        /** Parameter type */
        type?: Inline,

        /** Whether this parameter is required. */
        required?: boolean,

        /** Default value of this parameter */
        default?: Plaintext,

        /** Whether this parameter is deprecated */
        deprecated: boolean,

        /** Additional key value pair facts about this parameter. */
        fact?: { [key: Plaintext]: Inline }
    }[]
}

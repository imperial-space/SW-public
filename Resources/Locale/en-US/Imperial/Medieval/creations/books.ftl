ent-CreationsBook = book blank
.desc = A blank for creating a book that can be filled with text. What is written in it may be preserved for centuries.
.suffix = { "Medieval" }

creations-book-verb-send = Send

book-window-confirmation-title = Confirmation
book-window-confirmation-warning = Warning! Once you send the book, any changes will result in a new book and will not affect the already sent one.
book-window-confirmation-name-placeholder = Book title
book-window-confirmation-description-placeholder = Book description
book-window-confirmation-author-placeholder = Author
book-window-confirmation-send-button = Send

book-window-confirmation-name-length = The book title must be between {$min} and {$max} characters long.
book-window-confirmation-description-length = The book description must be up to {$max} characters long.
book-window-confirmation-author-length = The author name must be between {$min} and {$max} characters long.

ent-RandomCreationsBook = random book
    .desc = A random book that can be sent to creations.
    .suffix = { "Medieval" }

ent-BookshelfCreationsFilled = { ent-Bookshelf }
    .suffix = Filled, Random, Creations
    .desc = { ent-Bookshelf.desc }
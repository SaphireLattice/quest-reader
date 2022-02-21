class VisitAnalytics {
    landmarksObserver: IntersectionObserver;
    lazyloadObserver: IntersectionObserver;
    lastWidth: number = 0;
    resizeTimeout: number | null = null;
    images: HTMLImageElement[] = [];

    constructor() {
        window.plausible = window.plausible || function () { (window.plausible.q = window.plausible.q || []).push(arguments); };

        // Hopefull this won't miss anything
        if (document.readyState == "interactive")
            this.init()
        else
            document.addEventListener("DOMContentLoaded", () => this.init(), false);
    }

    init() {
        this.landmarksObserver = new IntersectionObserver(
            (entries, observer) => this.handleLandmarks(entries, observer),
            {
                root: null,
                rootMargin: "0px",
                threshold: 1.0
            }
        );
        this.lazyloadObserver = new IntersectionObserver(
            (entries, observer) => this.handleLazyload(entries, observer),
            {
                root: null,
                rootMargin: "75% 0px 75% 0px",
                threshold: 0.1
            }
        );

        {
            var all = document.querySelectorAll(".chapter-announce");
            all.forEach(elem => this.landmarksObserver.observe(elem));
            this.landmarksObserver.observe(document.querySelector("footer"));
        }

        {
            var all = document.querySelectorAll(".image-post");
            all.forEach(elem => {
                this.lazyloadObserver.observe(elem);
                this.images.push(elem.querySelector(".post-image > img"));
            });

            this.resizeTimeout = setTimeout(() => this.resizeDebounced(), 200);
            window.addEventListener("resize", (event) => this.handleResize(event));
        }

        console.log("Event handlers and observers ready");
    }

    handleLandmarks(entries: IntersectionObserverEntry[], observer: IntersectionObserver) {
        entries.filter(e => e.isIntersecting).forEach(e => {
                const announcingPost = e.target.getAttribute("data-announcing-post");

                if (announcingPost != null && !e.target.parentElement.querySelector<HTMLImageElement>(`#post-${announcingPost} img`).complete)
                    return;
                window.plausible("landmark", {props: {id: e.target.id ? e.target.id : e.target.tagName}});
                observer.unobserve(e.target);
                console.log("Reached landmark " + e.target.id ? e.target.id : e.target.tagName);
            }
        );
    }

    resizeDebounced() {
        clearTimeout(this.resizeTimeout);
        this.resizeTimeout = null;

        const elem = document.querySelector(".post-content");
        // Note: depends on main.css
        const elemWidth = Math.floor(elem.clientWidth * 0.95);

        if (elemWidth != this.lastWidth) {
            this.lastWidth = elemWidth;
            this.images.forEach(img => {
                const naturalHeight = +img.getAttribute("data-height");
                const naturalWidth = +img.getAttribute("data-width");
                img.height = Math.floor(naturalHeight * Math.min(1, elemWidth / naturalWidth));
            })
        }
    }

    handleResize(event) {
        if (event && event.type == "resize") {
            if (this.resizeTimeout != null)
                clearTimeout(this.resizeTimeout);
            this.resizeTimeout = setTimeout(() => this.resizeDebounced(), 200);
        }
    }

    handleLazyload(entries: IntersectionObserverEntry[], observer: IntersectionObserver) {
        entries.filter(e => e.isIntersecting).forEach(e => {
                const imgElem = e.target.querySelector<HTMLImageElement>(`img`);

                imgElem.addEventListener("load", () => {
                    imgElem.classList.add("loaded");
                });
                imgElem.src = imgElem.getAttribute("data-src");

                observer.unobserve(e.target);
                console.log(`Lazyloaded ${ e.target.id ? e.target.id : e.target.tagName }`);
            }
        );
    }
}
new VisitAnalytics();
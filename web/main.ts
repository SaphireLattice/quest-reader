class VisitAnalytics {
    landmarksObserver: IntersectionObserver;

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

        {
            var all = document.querySelectorAll(".chapter-announce");
            all.forEach(elem => this.landmarksObserver.observe(elem));
            this.landmarksObserver.observe(document.querySelector("footer"));
        }

        console.log("Intersection observer ready");
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
}
new VisitAnalytics();
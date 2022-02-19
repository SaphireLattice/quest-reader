class VisitAnalytics {
    observer;

    constructor() {
        window.plausible = window.plausible || function () { (window.plausible.q = window.plausible.q || []).push(arguments); };
        if (document.readyState == "interactive")
            this.init()
        else
            document.addEventListener("DOMContentLoaded", () => this.init(), false);
    }

    init() {
        let options = {
            root: null,
            rootMargin: "0px",
            threshold: 1.0
        };

        this.observer = new IntersectionObserver((entries, observer) => this.handleIntersect(entries, observer), options);

        var all = document.querySelectorAll(".chapter-announce");
        all.forEach(elem => this.observer.observe(elem));
        this.observer.observe(document.querySelector("footer"));
        console.log("Intersection observer ready");
    }

    handleIntersect(entries: IntersectionObserverEntry[], observer: IntersectionObserver) {
        entries.filter(e => e.isIntersecting).forEach(e => {
                if (e.target.className == "chapter-announce" && !e.target.nextElementSibling.querySelector("img").complete)
                    return;
                window.plausible("landmark", {props: {id: e.target.id ? e.target.id : e.target.tagName}});
                observer.unobserve(e.target);
                console.log("Reached landmark " + e.target.id ? e.target.id : e.target.tagName);
            }
        );
    }
}
new VisitAnalytics();